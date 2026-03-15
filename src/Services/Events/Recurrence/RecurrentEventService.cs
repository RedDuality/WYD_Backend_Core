using MongoDB.Bson;
using MongoDB.Driver;
using Ical.Net.DataTypes;

using Core.Components.Database;
using Core.Model.Util;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Services.Profiles;
using Core.Components.MessageQueue;
using Core.Model.QueueMessages;
using Core.Services.Util;
using Core.Model.Util.Exceptions;

namespace Core.Services.Events.Recurrence;

public class RecurrentEventService(
    MongoDbService dbService,
    EventDetailsService eventDetailsService,
    ProfileRecurrentEventService profileEventService,
    IMessageQueueService messageService
)
{
    private readonly CollectionName recurrentEventCollection = CollectionName.RecurrentEvents;

    #region expand

    /// Expands a RecurrentEvent into individual Event instances for all occurrences
    /// that overlap with the given [startTime, endTime] window, then maps each to a DTO.
    private static List<RetrieveEventResponseDto> ExpandRecurrentEvent(
        RecurrentEvent ev,
        ObjectId profileId,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        TimeSpan eventDuration = ev.EndTime - ev.StartTime;

        var dtos = RecurrenceService.GetOccurrences(
            ev.RecurrenceRule,
            ev.StartTime,
            ev.RecurrenceEnd,
            ev.TimeZone,
            Duration.FromTimeSpanExact(eventDuration),
            startTime,
            endTime)
            .Select(occurrenceStart => BuildEventInstance(ev, occurrenceStart, occurrenceStart.Add(eventDuration)))
            .Select(instanceEvent => new RetrieveEventResponseDto(
                instanceEvent,
                profileEventDtos: [
                    new ProfileEventDto {
                        ProfileId = profileId.ToString(),
                        Role = EventRole.Owner,
                        Confirmed = true,
                        Trusted = true
                    }
                ]
            ))
            .ToList();

        return dtos;
    }

    /// Builds a transient (non-persisted) Event for one recurrence occurrence,
    /// copying all relevant fields from the master RecurrentEvent.
    private static Event BuildEventInstance(
        RecurrentEvent master,
        DateTimeOffset occurrenceStart,
        DateTimeOffset occurrenceEnd)
    {
        // Use compact ISO-8601 UTC instant as the instance identifier —
        // uniquely identifies this slot within the recurrence series.
        var instanceId = master.IsAllDay
            ? occurrenceStart.UtcDateTime.ToString("yyyyMMdd")       // DATE
            : occurrenceStart.UtcDateTime.ToString("yyyyMMddTHHmmssZ"); // DATE-TIME

        return new Event(master.Title, occurrenceStart, occurrenceEnd)
        {
            UpdatedAt = master.UpdatedAt,
            IsAllDay = master.IsAllDay,
            MasterEventId = master.Id,     // links back to the RecurrentEvent
            RecurrencyInstanceId = instanceId,
            DetachedInstance = false,
            ImportedAccountUid = master.ImportedAccountUid,
            ExternalEventId = null,
            ExternalMasterEventId = master.ExternalEventId,
        };
    }
    #endregion

    #region create
    public async Task<List<RetrieveEventResponseDto>> CreateRecurrentEventAsync(CreateRecurrentEventRequestDto newEventDto, Profile creatorProfile)
    {
        var ev = new RecurrentEvent(
            newEventDto.Title,
            newEventDto.StartTime,
            newEventDto.EndTime,
            newEventDto.GetTimeZoneInfo(),
            newEventDto.RecurrenceRule);

        List<RetrieveEventResponseDto> InstanceDtos = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            await dbService.CreateOneAsync(recurrentEventCollection, ev, session);
            EventDetails eventDetails = await eventDetailsService.CreateAsync(ev, newEventDto.Description, session);
            ProfileRecurrentEvent profileRecurrentEvent = await profileEventService.CreateProfileEventAsync(ev, creatorProfile.Id, session);

            await SendCreatePropagationMessage(ev, creatorProfile);

            return ExpandRecurrentEvent(ev, creatorProfile.Id, newEventDto.CacheIntervalStart, newEventDto.CacheIntervalEnd);
        });
        return InstanceDtos;
    }

    private async Task SendCreatePropagationMessage(RecurrentEvent ev, Profile creatorProfile)
    {
        var propagationMessage = new QueueMessage<RecurrentEventPayload>(
                MessageType.recurrentEventUpdate,
                new(ev, EventUpdateType.create, actorId: creatorProfile.Id.ToString())
            );
        await messageService.SendPropagationMessageAsync(propagationMessage);
    }
    #endregion

    #region retrieve
    public async Task<RetrieveEventResponseDto> RetrieveDetailsById(RetrieveRecurrenceInstanceDetailsRequestDto requestDto)
    {
        // check if an instance was created over the generated one.
        var possibleInstance = await CheckPossibleInstanceId(requestDto);
        if (possibleInstance != null)
            return possibleInstance;

        // if not, check if master still covers that.
        // if it covers, return the generated instance + event details
        return await GetGeneratedInstance(requestDto);

    }

    private async Task<RetrieveEventResponseDto?> CheckPossibleInstanceId(RetrieveRecurrenceInstanceDetailsRequestDto retrieveDto)
    {
        var filter = Builders<Event>.Filter.And(
            Builders<Event>.Filter.Eq(e => e.MasterEventId, new ObjectId(retrieveDto.MasterEventId)),
            Builders<Event>.Filter.Eq(e => e.RecurrencyInstanceId, retrieveDto.RecurrencyInstanceId)
        );

        var ev = await dbService.RetrieveOrNullAsync(CollectionName.Events, filter);
        if (ev != null)
        {
            var eventDetails = await eventDetailsService.RetrieveByEventId(ev.Id.ToString());
            return new RetrieveEventResponseDto(ev, details: eventDetails);
        }
        return null;
    }

    private async Task<RetrieveEventResponseDto> GetGeneratedInstance(RetrieveRecurrenceInstanceDetailsRequestDto requestDto)
    {
        var master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, requestDto.MasterEventId);

        DateTimeOffset occurrenceStart = ParseInstanceId(requestDto.RecurrencyInstanceId, master.TimeZone);
        TimeSpan eventDuration = master.EndTime - master.StartTime;

        // Verify the master's recurrence rule still generates this occurrence.
        // We query a window of [occurrenceStart, occurrenceStart + duration] so only
        // the exact slot can match — if the rule was shortened or the instance was
        // deleted, GetOccurrences returns empty and we surface ObjectDeletedException.
        bool occurrenceExists = RecurrenceService
            .GetOccurrences(
                master.RecurrenceRule,
                master.StartTime,
                master.RecurrenceEnd,
                master.TimeZone,
                Duration.FromTimeSpanExact(eventDuration),
                occurrenceStart,
                occurrenceStart + eventDuration)
            .Any();

        if (!occurrenceExists)
            throw new ObjectDeletedException();

        Event eventInstance = BuildEventInstance(master, occurrenceStart, occurrenceStart.Add(eventDuration));

        // EventDetails are stored against the master event, not individual instances.
        var eventDetails = await eventDetailsService.RetrieveByEventId(requestDto.MasterEventId);
        return new RetrieveEventResponseDto(eventInstance, details: eventDetails);
    }

    /// Reverses the compact ISO-8601 instance ID produced in <see cref="BuildEventInstance"/>
    /// back into a <see cref="DateTimeOffset"/>.
    /// DATE format:      yyyyMMdd         → interpreted in the event's local time zone
    /// DATE-TIME format: yyyyMMddTHHmmssZ → UTC instant
    private static DateTimeOffset ParseInstanceId(string instanceId, TimeZoneInfo timeZone)
    {
        if (instanceId.Length == 8) // DATE: yyyyMMdd
        {
            var date = DateTime.ParseExact(
                instanceId,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None);

            return new DateTimeOffset(date, timeZone.GetUtcOffset(date));
        }
        else // DATE-TIME: yyyyMMddTHHmmssZ
        {
            var utcDt = DateTime.ParseExact(
                instanceId,
                "yyyyMMddTHHmmssZ",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

            return new DateTimeOffset(utcDt, TimeSpan.Zero);
        }
    }

    public async Task<List<RetrieveEventResponseDto>> RetrieveEventsByProfileIds(RetrieveMultipleEventsRequestDto requestDto)
    {
        // create pipeline, to have the db handle everything in one operation
        var aggregate = dbService.GetAggregate<ProfileRecurrentEvent>(CollectionName.ProfileRecurrentEvents);

        var objectIds = requestDto.ProfileIds.Select(ph => new ObjectId(ph)).ToList();

        var filter = Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.In(pe => pe.ProfileId, objectIds),
            Builders<ProfileRecurrentEvent>.Filter.Gte(pe => pe.RecurrenceEnd, requestDto.StartTime.ToUniversalTime()),
            Builders<ProfileRecurrentEvent>.Filter.Lte(pe => pe.RecurrenceStart, requestDto.EndTime.ToUniversalTime())
        );

        // Apply the filter to the aggregate pipeline
        var profileEvents = aggregate.Match(filter).Limit(100);

        // Step 3: lookup to RecurrentEvent collection, and
        // join the two collections in a ProfileEventWithCorrEvents object
        var lookupStage = profileEvents.Lookup<ProfileRecurrentEvent, RecurrentEvent, ProfileRecurrentEventWithCorrespondingEvents>(
            dbService.GetCollection<RecurrentEvent>(recurrentEventCollection),
            pe => pe.EventId,
            e => e.Id,
            prewce => prewce.Events);

        //flat out the results on a new projected object (profileEvent only have one event)
        var projected = lookupStage
            .Project(prewce => new EventWithProfile(prewce.Events[0], prewce.ProfileId));

        var result = await projected.ToListAsync();


        // return expanded events
        return result.SelectMany(
            ewp => ExpandRecurrentEvent(
                ewp.Event,
                ewp.ProfileId,
                requestDto.StartTime,
                requestDto.EndTime)
            ).ToList();
    }

    #endregion

}
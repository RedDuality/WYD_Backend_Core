using MongoDB.Bson;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Util;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Services.Profiles;
using Core.Services.Events.Recurrence;
using Core.Services.Events.Instances;

namespace Core.Services.Events;

public class EventRetrieveService(
    MongoDbService dbService,
    EventDetailsService eventDetailsService,
    ProfileEventService profileEventService,
    EventProfileService eventProfileService,

    EventService instancesEventService,
    RecurrentEventService recurrentEventService
)
{
    private readonly CollectionName eventCollection = CollectionName.Events;


    #region retrieve
    public async Task CheckEventExists(string id)
    {
        await dbService.ConfirmExists<Event>(eventCollection, id);
    }

    // for RT updates(creation/share of an event)
    public async Task<RetrieveEventResponseDto> RetrieveEventById(string eventId, string profileId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var pe = await profileEventService.FindByProfileAndEventId(profileId, eventId);
        return new RetrieveEventResponseDto(ev, profileEvents: [pe!]);
    }

    public async Task<RetrieveEventResponseDto> RetrieveEventWithDetailsById(string eventId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var eventDetails = await eventDetailsService.RetrieveByEventId(eventId);
        return new RetrieveEventResponseDto(ev, details: eventDetails);
    }

    public async Task<List<RetrieveEventResponseDto>> RetrieveEventsByProfileIds(RetrieveMultipleEventsRequestDto requestDto)
    {
        var instancesTask = instancesEventService.RetrieveEventsByProfileIds(requestDto);
        var recurrenceTask = recurrentEventService.RetrieveEventsByProfileIds(requestDto);

        var results = await Task.WhenAll(instancesTask, recurrenceTask);
        return SubstituteWithInstances(results[0], results[1]);
    }

    // filter out edited recurrence Instances
    private static List<RetrieveEventResponseDto> SubstituteWithInstances(
        List<RetrieveEventResponseDto> instanceEvents,
        List<RetrieveEventResponseDto> recurrenceEvents)
    {
        if (recurrenceEvents.Count > 0)
        {
            var overriddenInstanceIds = instanceEvents
                .Where(e => e.RecurrencyInstanceId != null)
                .Select(e => e.RecurrencyInstanceId)
                .ToHashSet();

            recurrenceEvents = recurrenceEvents
                .Where(e => e.RecurrencyInstanceId == null || !overriddenInstanceIds.Contains(e.RecurrencyInstanceId))
                .ToList();
        }
        return instanceEvents.Concat(recurrenceEvents).ToList();
    }

    public async Task<List<RetrieveEventResponseDto>> RetrieveUpdatesByProfileIds(RetrieveUpdatedEventsRequestDto requestDto)
    {
        var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

        var objectIds = requestDto.ProfileIds.Select(ph => new ObjectId(ph)).ToList();

        var filterBuilder = Builders<ProfileEvent>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.In(pe => pe.ProfileId, objectIds),
            filterBuilder.Gte(pe => pe.UpdatedAt, requestDto.UpdatedAfterTime.ToUniversalTime())
         );

        // Apply the filter to the aggregate pipeline
        var matchStage = aggregate.Match(filter);

        // Step 3: Lookup the corresponding Event for each ProfileEvent
        // Join the two collecions in a ProfileEventWithCEvents object
        var lookupStage = matchStage.Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
            dbService.GetCollection<Event>(eventCollection),
            pe => pe.EventId,
            e => e.Id,
            pewce => pewce.Events);

        //flat out the results on a new projected object
        var projected = lookupStage
            .Project(pe => new
            {
                Event = pe.Events[0],
                pe.ProfileId,
                pe.Role,
                pe.Confirmed
            });


        //var intermediateResults = await projected.ToListAsync();

        var grouped = projected.Group(
            pe => pe.Event.Id,
            group => new
            {
                ev = group.First().Event,
                ProfileEvents = group.Select(pe => new ProfileEventDto
                {
                    ProfileId = pe.ProfileId.ToString(),
                    Role = pe.Role,
                    Confirmed = pe.Confirmed,
                    Trusted = false
                }).ToList()
            }
        );

        var result = await grouped.ToListAsync();

        // Map the results over EventDto objects
        var finalResult = result.Select(g => new RetrieveEventResponseDto(g.ev, profileEventDtos: g.ProfileEvents)).ToList();

        return finalResult;

    }

    public async Task<HashSet<ProfileEventDto>> GetProfileEventsAsync(string eventId)
    {
        var eps = await eventProfileService.FindAllByEventId(new ObjectId(eventId));

        // Build the (profileId, eventId) pairs
        var profileEventPairs = eps
            .Select(ep => (ep.ProfileId.ToString(), eventId))
            .ToList();

        return await profileEventService.FindMultipleByProfileAndEventIds(profileEventPairs);
    }

    #endregion
}
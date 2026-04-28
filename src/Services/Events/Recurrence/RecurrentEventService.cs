using MongoDB.Bson;
using MongoDB.Driver;
using Ical.Net.DataTypes;

using Core.Components.Database;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Services.Profiles;
using Core.Components.MessageQueue;
using Core.Model.QueueMessages;
using Core.Services.Util;
using Core.Model.Util.Exceptions;
using Core.Model.Util.EventsQuery;
using Core.Services.Events.Instances;

namespace Core.Services.Events.Recurrence;

public class RecurrentEventService(
    MongoDbService dbService,
    EventService eventService,
    EventDetailsService eventDetailsService,
    ProfileRecurrentEventService profileEventService,
    IMessageQueueService messageService
)
{
    private readonly CollectionName recurrentEventCollection = CollectionName.RecurrentEvents;
    private readonly CollectionName detachedInstancesCollection = CollectionName.DetachedInstances;

    #region create
    public async Task<RetrieveRecurrentEventResponseDto> CreateRecurrentEventAsync(CreateRecurrentEventRequestDto newEventDto, Profile creatorProfile)
    {
        var ev = new RecurrentEvent(
            newEventDto.Title,
            newEventDto.StartTime,
            newEventDto.EndTime,
            newEventDto.GetTimeZoneInfo(),
            newEventDto.RecurrenceRule);

        RetrieveRecurrentEventResponseDto InstanceDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var (newEvent, eventDetails, profileRecurrentEvent) = await CreateRecurrentEvent(ev, creatorProfile, [], newEventDto.Description, session);

            return new RetrieveRecurrentEventResponseDto(newEvent, eventDetails, profileEventDtos: [new ProfileEventDto
                {
                    ProfileId = profileRecurrentEvent.ProfileId.ToString(),
                    Role = profileRecurrentEvent.Role,
                    Confirmed = profileRecurrentEvent.Confirmed,
                    Trusted = false
                }]);
        });
        return InstanceDto;
    }

    private async Task<(RecurrentEvent, EventDetails, ProfileRecurrentEvent)> CreateRecurrentEvent(
        RecurrentEvent ev,
        Profile creatorProfile,
        HashSet<ObjectId> otherProfilesIds,
        string? description,
        IClientSessionHandle session)
    {
        await dbService.CreateOneAsync(recurrentEventCollection, ev, session);
        EventDetails eventDetails = await eventDetailsService.CreateAsync(ev, description, session);
        ProfileRecurrentEvent profileRecurrentEvent = await profileEventService.CreateProfileEventAsync(ev, creatorProfile.Id, session, role: EventRole.Owner);

        await SendCreatePropagationMessage(ev, creatorProfile);
        return (ev, eventDetails, profileRecurrentEvent);
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

    #region modify
    public async Task<RetrieveRecurrentEventResponseDto> UpdateRecurrentEvent(UpdateRecurrentEventRequestDto updateDto, Profile profile)
    {
        switch (updateDto.UpdateType)
        {
            case RecurrentUpdateType.AllTheSequence:
                // update the master

                var master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);
                var updates = GetUpdates(updateDto);

                EventDetails? details = null;

                var upatedEvent = await dbService.ExecuteInTransactionAsync(async (session) =>
                    {
                        if (updateDto.Description != null)
                        {
                            details = await eventDetailsService.Update(master.Id, updateDto.Description, session);
                        }

                        // Check if there are any updates to perform
                        if (updates.Count != 0)
                        {
                            var combinedUpdate = Builders<RecurrentEvent>.Update.Combine(updates);

                            master = await dbService.FindOneByIdAndUpdateAsync(recurrentEventCollection, master.Id, combinedUpdate, session);

                            var propagationMessage = new QueueMessage<RecurrentEventPayload>(MessageType.eventUpdate, new(master, EventUpdateType.update));
                            await messageService.SendPropagationMessageAsync(propagationMessage);
                        }

                        return master;
                    });
                return new RetrieveRecurrentEventResponseDto(upatedEvent, details: details);

            case RecurrentUpdateType.ThisInstance:
                if (updateDto.RecurrenceRule != null && updateDto.RecurrenceRule.Length > 0)
                    throw new ArgumentException("Cannot edit the recurrency rule for a single instance");

                if (updateDto.InstanceEventId.Contains(updateDto.MasterEventId)) //generated event
                {
                    // check DetachedInstances   
                    var filter = Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, new ObjectId(updateDto.MasterEventId));
                    var detachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, filter);

                    if (detachedInstances != null && detachedInstances.Instances.Any(i => i.RecurrencyId == updateDto.InstanceEventId))
                        throw new ArgumentException("There already exists a detached instance for this slot");

                    var masterEvent = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

                    // 1. Sanify: Validate the ID and extract the date part
                    if (!RecurrenceService.CheckRecurrencyIdIsValid(
                        masterEvent.RecurrenceRule,
                        masterEvent.StartTime,
                        masterEvent.EndTime,
                        masterEvent.RecurrenceEnd,
                        masterEvent.TimeZone,
                        updateDto.InstanceEventId,
                        out string datePart)
                    )
                    {
                        throw new ArgumentException("The provided InstanceEventId is not a valid occurrence of this series.");
                    }

                    // 1. Determine the "original" times for this specific slot
                    DateTimeOffset originalOccurrenceStart = RecurrenceService.ParseInstanceId(datePart, masterEvent.TimeZone);
                    TimeSpan masterDuration = masterEvent.EndTime - masterEvent.StartTime;
                    DateTimeOffset originalOccurrenceEnd = originalOccurrenceStart.Add(masterDuration);

                    // 2. Determine the final intended times
                    DateTimeOffset finalStart = updateDto.StartTime ?? originalOccurrenceStart;
                    DateTimeOffset finalEnd = updateDto.EndTime ?? originalOccurrenceEnd;

                    // 3. Safety Check: If user updated only one, ensure the duration is at least preserved or valid
                    if (updateDto.StartTime.HasValue && !updateDto.EndTime.HasValue)
                    {
                        // If user moved the start but didn't specify end, 
                        // maintain the original duration relative to the new start
                        finalEnd = finalStart.Add(masterDuration);
                    }

                    if (finalEnd <= finalStart)
                    {
                        throw new ArgumentException("End time must be after start time.");
                    }

                    var newInstance = new Event(
                        updateDto.Title ?? masterEvent.Title,
                        finalStart,
                        finalEnd)
                    {
                        // Important: Use only the datePart (yyyyMMdd...) not the full Compound ID
                        RecurrencyInstanceId = datePart, 
                        MasterEventId = masterEvent.Id,
                        DetachedInstance = true,
                    };

                    // create new event
                    await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
                    {
                        var (newDetachedInstance, details, profileEvent) = await eventService.CreateEvent(newInstance, profile, [], updateDto.Description, session);

                        // update DetachedInstances
                        var detachedInstance = new DetachedInstance(newDetachedInstance.Id, newDetachedInstance.RecurrencyInstanceId!, newDetachedInstance.StartTime);
                        if (detachedInstances == null)
                        {
                            var instances = new DetachedInstances(new ObjectId(updateDto.MasterEventId), [detachedInstance]);
                            await dbService.CreateOneAsync(detachedInstancesCollection, instances, session: null);
                        }
                        else
                        {
                            var update = Builders<DetachedInstances>.Update.AddToSet(x => x.Instances, detachedInstance);
                            await dbService.UpdateOneByIdAsync(detachedInstancesCollection, detachedInstances.Id, update);
                        }

                        return null;
                    });
                }
                else
                { //detached instance
                    
                    var singleUpdateDto = new UpdateEventRequestDto()
                    {
                        EventId = updateDto.InstanceEventId,
                        Title = updateDto.Title,
                        Description = updateDto.Description,
                        StartTime = updateDto.StartTime,
                        EndTime = updateDto.StartTime
                    };

                    var dto = await dbService.ExecuteInTransactionAsync(async (session) =>
                    {
                        var (ev, details) = await eventService.UpdateEvent(singleUpdateDto, session);

                        if (updateDto.StartTime != null)
                        {
                            // update detachedInstances
                            var filter = Builders<DetachedInstances>.Filter.And(
                                Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(updateDto.MasterEventId)),
                                Builders<DetachedInstances>.Filter.ElemMatch(di => di.Instances, i => i.RecurrencyId == updateDto.InstanceEventId)
                            );
                            var update = Builders<DetachedInstances>.Update.Set("instances.$.startTime", updateDto.StartTime);

                            await dbService.UpdateOneAsync(detachedInstancesCollection, filter, update, session);
                        }

                        return new RetrieveEventResponseDto(ev, details: details);
                    });
                }
                break;
            case RecurrentUpdateType.ThisAndAllFollowing:

                // stop previous masterevent
                var oldMaster = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

                DateTimeOffset stopTime;
                if (updateDto.StartTime != null)
                    stopTime = updateDto.StartTime.Value.ToUniversalTime();
                else if (updateDto.InstanceEventId.Contains(updateDto.MasterEventId)) // generated
                {
                    // deduce it from the instanceId
                    stopTime = RecurrenceService.ParseInstanceId(updateDto.InstanceEventId, oldMaster.TimeZone);
                }
                else // detached instance
                {
                    var det = await dbService.RetrieveByIdAsync<Event>(CollectionName.Events, updateDto.InstanceEventId);
                    stopTime = det.StartTime;
                }


                var newRecurrenceEnd = stopTime;
                var truncatedRule = RecurrenceService.TruncateRuleUntil(oldMaster.RecurrenceRule, stopTime, oldMaster.TimeZone);
                var oldMasterUpdates = new List<UpdateDefinition<RecurrentEvent>>
                {
                    Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceRule, truncatedRule),
                    Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceEnd, newRecurrenceEnd)
                };

                // create new master
                var newNewRecurrenceRule = updateDto.RecurrenceRule ?? oldMaster.RecurrenceRule;
                var newMaster = new RecurrentEvent(
                    updateDto.Title ?? oldMaster.Title,
                    updateDto.StartTime ?? oldMaster.StartTime,
                    updateDto.EndTime ?? oldMaster.EndTime,
                    oldMaster.TimeZone,
                    newNewRecurrenceRule);

                var description = updateDto.Description
                    ?? (await dbService.RetrieveAsync(
                           CollectionName.EventDetails,
                           Builders<EventDetails>.Filter.Eq(d => d.EventId, oldMaster.Id)))
                       .Description;
                var singleEventDuration = new Duration((int)(newMaster.EndTime - newMaster.StartTime).TotalMinutes);

                RetrieveRecurrentEventResponseDto eventDto = await dbService.ExecuteInTransactionAsync(async (session) =>
                {
                    await dbService.UpdateOneByIdAsync(recurrentEventCollection, oldMaster.Id, Builders<RecurrentEvent>.Update.Combine(oldMasterUpdates), session);

                    var (newEvent, eventDetails, profileRecurrentEvent) = await CreateRecurrentEvent(newMaster, profile, [], description, session);

                    var response = new RetrieveRecurrentEventResponseDto(newEvent, eventDetails, profileEventDtos: [new ProfileEventDto
                        {
                            ProfileId = profileRecurrentEvent.ProfileId.ToString(),
                            Role = profileRecurrentEvent.Role,
                            Confirmed = profileRecurrentEvent.Confirmed,
                            Trusted = false
                        }]);

                    // update the two DetachedInstances
                    var oldMasterFilter = Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, new ObjectId(updateDto.MasterEventId));
                    var oldDetachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, oldMasterFilter);

                    var allOldInstances = oldDetachedInstances?.Instances ?? [];

                    // Split on cut-off
                    var stayWithOldMaster = allOldInstances.Where(i => i.StartTime <= stopTime).ToHashSet();
                    var migrateToNewMaster = allOldInstances.Where(i => i.StartTime > stopTime).ToHashSet();

                    if (oldDetachedInstances != null)
                    {
                        await dbService.UpdateOneByIdAsync(detachedInstancesCollection, oldDetachedInstances.Id, Builders<DetachedInstances>.Update.Set(x => x.Instances, stayWithOldMaster), session);
                    }

                    if (migrateToNewMaster.Count > 0)
                    {
                        var remappedInstances = new HashSet<DetachedInstance>();

                        // update master and recurrence IDs in "after the date" detached instances
                        foreach (var i in migrateToNewMaster)
                        {
                            // Parse the OLD scheduled time from the existing recurrencyId
                            var originalOccurrenceStartTime = RecurrenceService.ParseInstanceId(i.RecurrencyId, oldMaster.TimeZone);

                            // Find the nearest slot in the NEW series
                            string newRecurrencyId =
                                RecurrenceService.FindCorrespondingInstanceId(
                                    newMaster.RecurrenceRule,
                                    newMaster.StartTime,
                                    newMaster.RecurrenceEnd,
                                    newMaster.TimeZone,
                                    singleEventDuration,
                                    originalOccurrenceStartTime)
                                // If genuinely no nearby occurrence exists (e.g. the series ends
                                // before this instance's week), keep the old id — the instance
                                // becomes a free-standing event and won't block anything.
                                ?? i.RecurrencyId;

                            List<UpdateDefinition<Event>> detachedUpdates = [];
                            detachedUpdates.Add(Builders<Event>.Update.Set(e => e.RecurrencyInstanceId, newRecurrencyId));
                            detachedUpdates.Add(Builders<Event>.Update.Set(e => e.MasterEventId, newEvent.Id));

                            if (updateDto.InstanceEventId.Equals(i.EventId)) // current instance is detached instance
                            {
                                detachedUpdates.AddRange(GetInstanceUpdates(updateDto));

                                var detailsFilter = Builders<EventDetails>.Filter.Eq(d => d.EventId, i.EventId);
                                await dbService.UpdateOneAsync(CollectionName.EventDetails, detailsFilter, Builders<EventDetails>.Update.Set(d => d.Description, updateDto.Description ?? ""), session);
                            }

                            await dbService.UpdateOneByIdAsync(CollectionName.Events, i.EventId, Builders<Event>.Update.Combine(detachedUpdates), session);

                            remappedInstances.Add(new DetachedInstance(i.EventId, newRecurrencyId, i.StartTime));
                        }

                        var instances = new DetachedInstances(new ObjectId(updateDto.MasterEventId), remappedInstances);
                        await dbService.CreateOneAsync(detachedInstancesCollection, instances, session: null);
                    }

                    return response;
                });
                return eventDto;
        }
    }

    private static List<UpdateDefinition<Event>> GetInstanceUpdates(UpdateRecurrentEventRequestDto updateDto)
    {
        var updates = new List<UpdateDefinition<Event>>();

        // Add updates to the list based on non-null values
        if (updateDto.Title != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.Title, updateDto.Title));
        }

        if (updateDto.StartTime != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.StartTime, updateDto.StartTime));
        }

        if (updateDto.EndTime != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.EndTime, updateDto.EndTime));
        }

        return updates;
    }

    private static List<UpdateDefinition<RecurrentEvent>> GetUpdates(UpdateRecurrentEventRequestDto updateDto)
    {
        var updates = new List<UpdateDefinition<RecurrentEvent>>();

        // Add updates to the list based on non-null values
        if (updateDto.Title != null)
        {
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.Title, updateDto.Title));
        }

        if (updateDto.StartTime != null)
        {
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.StartTime, updateDto.StartTime));
        }

        if (updateDto.EndTime != null)
        {
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.EndTime, updateDto.EndTime));
        }

        if (updateDto.RecurrenceRule != null)
        {
            string validRule = RecurrenceService.GetValidRule(updateDto.RecurrenceRule);
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.RecurrenceRule, validRule));
        }

        return updates;
    }

    #endregion

    #region retrieve
    public async Task<RetrieveEventResponseDto> RetrieveDetailsById(Profile profile, RetrieveRecurrenceInstanceDetailsRequestDto requestDto)
    {
        // check if an instance was created over the generated one.
        var possibleInstance = await CheckIfDetachedInstance(requestDto);
        if (possibleInstance != null)
            return possibleInstance;

        // if not, check if master still covers that.
        // if it covers, return the generated instance + event details
        return await GetGeneratedInstance(profile, requestDto);
    }

    private async Task<RetrieveEventResponseDto?> CheckIfDetachedInstance(RetrieveRecurrenceInstanceDetailsRequestDto retrieveDto)
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

    private async Task<RetrieveEventResponseDto> GetGeneratedInstance(Profile profile, RetrieveRecurrenceInstanceDetailsRequestDto requestDto)
    {
        var master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, requestDto.MasterEventId);

        DateTimeOffset occurrenceStart = RecurrenceService.ParseInstanceId(requestDto.RecurrencyInstanceId, master.TimeZone);
        TimeSpan eventDuration = master.EndTime - master.StartTime;

        // TODO check no detachedInstance has been created over this instanceId (detachedInstancesCollection)


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

        Event eventInstance = GenerateRecurrenceInstance(master, occurrenceStart, occurrenceStart.Add(eventDuration));

        // EventDetails are stored against the master event, not individual instances.
        var eventDetails = await eventDetailsService.RetrieveByEventId(requestDto.MasterEventId);
        return GetGeneratedEventDto(eventInstance, profile.Id, eventDetails);
    }

    private static RetrieveEventResponseDto GetGeneratedEventDto(Event generated, ObjectId profileId, EventDetails? details = null)
    {
        return new RetrieveEventResponseDto(
                generated,
                details: details,
                profileEventDtos: [
                    new ProfileEventDto {
                        ProfileId = profileId.ToString(),
                        Role = EventRole.Owner,
                        Confirmed = true,
                        Trusted = true
                    }
                ]
            )
        { Id = generated.MasterEventId.ToString() + '_' + generated.RecurrencyInstanceId };
    }

    /// Generates a transient (non-persisted) Event for one recurrence occurrence,
    /// copying all relevant fields from the master RecurrentEvent.
    private static Event GenerateRecurrenceInstance(
        RecurrentEvent master,
        DateTimeOffset occurrenceStart,
        DateTimeOffset occurrenceEnd)
    {
        var instanceId = master.IsAllDay
            ? occurrenceStart.UtcDateTime.ToString("yyyyMMdd")       // DATE
            : occurrenceStart.UtcDateTime.ToString("yyyyMMddTHHmmssZ"); // DATE-TIME

        return new Event(master.Title, occurrenceStart, occurrenceEnd)
        {
            UpdatedAt = master.UpdatedAt,
            IsAllDay = master.IsAllDay,
            MasterEventId = master.Id,
            RecurrencyInstanceId = instanceId,
            DetachedInstance = false,
            ImportedAccountUid = master.ImportedAccountUid,
            ExternalEventId = null,
            ExternalMasterEventId = master.ExternalEventId,
        };
    }

    public async Task<List<RetrieveRecurrentEventResponseDto>> RetrieveMastersByProfileIds(List<ObjectId> profileIds, RetrieveMultipleEventsRequestDto requestDto)
    {
        // create pipeline, to have the db handle everything in one operation
        var aggregate = dbService.GetAggregate<ProfileRecurrentEvent>(CollectionName.ProfileRecurrentEvents);

        var filter = Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.In(pe => pe.ProfileId, profileIds),
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
            .Project(prewce => new
            {
                Event = prewce.Events[0],
                prewce.ProfileId,
                prewce.Role,
                prewce.Confirmed
            });


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

        var finalResult = result.Select(g => new RetrieveRecurrentEventResponseDto(g.ev, profileEventDtos: g.ProfileEvents)).ToList();

        return finalResult;
    }

    #endregion

}
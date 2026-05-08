using MongoDB.Bson;
using MongoDB.Driver;
using Ical.Net.DataTypes;

using Core.Components.Database;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Components.MessageQueue;
using Core.Model.QueueMessages;
using Core.Services.Util;
using Core.Services.Events.Instances;

namespace Core.Services.Events.Recurrence;

public class RecurrentEventUpdateService(
    MongoDbService dbService,
    EventService eventService,
    RecurrentEventService recurrentEventService,
    EventDetailsService eventDetailsService,
    IMessageQueueService messageService
) {
    private readonly CollectionName recurrentEventCollection = CollectionName.RecurrentEvents;
    private readonly CollectionName detachedInstancesCollection = CollectionName.DetachedInstances;

    #region single 
    public async Task<RetrieveEventResponseDto> UpdateSingleInstance(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        if (updateDto.UpdateType != RecurrentUpdateType.ThisInstance)
            throw new InvalidOperationException("Mismatch between the request and the update type");

        if (updateDto.RecurrenceRule != null && updateDto.RecurrenceRule.Length > 0)
            throw new ArgumentException("Cannot edit the recurrency rule for a single instance");

        if (updateDto.InstanceEventId.Contains(updateDto.MasterEventId)) //generated
            return await CreateDetachedInstance(updateDto, profile);
        else //detached
            return await UpdateDetachedInstance(updateDto, profile);
    }

    private async Task<RetrieveEventResponseDto> CreateDetachedInstance(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        var master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

        // Validate the instanceId and extract the date part
        if (!RecurrenceService.CheckRecurrencyIdIsValid(
            master.RecurrenceRule,
            master.StartTime,
            master.EndTime,
            master.RecurrenceEnd,
            master.TimeZone,
            updateDto.InstanceEventId,
            out string datePart)
        )
            throw new ArgumentException("The provided InstanceEventId is not a valid occurrence of this series.");

        // check master's DetachedInstances   
        var filter = Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, master.Id);
        var detachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, filter);

        if (detachedInstances != null && detachedInstances.Instances.Any(i => i.RecurrencyId == updateDto.InstanceEventId))
            throw new ArgumentException("There already exists a detached instance for this slot");

        // determine times
        DateTimeOffset originalOccurrenceStart = RecurrenceService.ParseInstanceId(datePart, master.TimeZone);
        TimeSpan masterDuration = master.EndTime - master.StartTime;
        DateTimeOffset originalOccurrenceEnd = originalOccurrenceStart.Add(masterDuration);

        DateTimeOffset finalStart = updateDto.StartTime ?? originalOccurrenceStart;
        DateTimeOffset finalEnd = updateDto.EndTime ?? originalOccurrenceEnd;

        if (updateDto.StartTime.HasValue && !updateDto.EndTime.HasValue)
            finalEnd = finalStart.Add(masterDuration);

        if (finalEnd <= finalStart)
            throw new ArgumentException("End time must be after start time.");


        var newInstance = new Event(
            updateDto.Title ?? master.Title,
            finalStart,
            finalEnd) {
            RecurrencyInstanceId = updateDto.InstanceEventId,
            MasterEventId = master.Id,
            DetachedInstance = true,
        };

        // create new event
        var (newDetachedInstance, details, profileEvent) = await dbService.ExecuteInTransactionAsync(async (session) => {
            var (newDetachedInstance, details, profileEvent) = await eventService.CreateEvent(newInstance, profile, [], updateDto.Description, session);

            // update DetachedInstances
            var singleDetachedInstance = new DetachedInstance(newDetachedInstance.Id, newDetachedInstance.RecurrencyInstanceId!, newDetachedInstance.StartTime);

            if (detachedInstances == null) {
                var instances = new DetachedInstances(new ObjectId(updateDto.MasterEventId), [singleDetachedInstance]);
                await dbService.CreateOneAsync(detachedInstancesCollection, instances, session: null);
            }
            else {
                var update = Builders<DetachedInstances>.Update.AddToSet(x => x.Instances, singleDetachedInstance);
                await dbService.UpdateOneByIdAsync(detachedInstancesCollection, detachedInstances.Id, update);
            }

            return (newDetachedInstance, details, profileEvent);
        });

        return new RetrieveEventResponseDto(newDetachedInstance, details: details, profileEvents: [profileEvent]);
    }

    private async Task<RetrieveEventResponseDto> UpdateDetachedInstance(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        // check master's DetachedInstances   
        var filter = Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, new ObjectId(updateDto.MasterEventId));
        var detachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, filter);

        if (detachedInstances == null || !detachedInstances.Instances.Any(i => i.EventId == new ObjectId(updateDto.InstanceEventId)))
            throw new ArgumentException("No detached instance found for the given master");

        var singleUpdateDto = new UpdateEventRequestDto() {
            EventId = updateDto.InstanceEventId,
            Title = updateDto.Title,
            Description = updateDto.Description,
            StartTime = updateDto.StartTime,
            EndTime = updateDto.StartTime
        };

        var (ev, details) = await dbService.ExecuteInTransactionAsync(async (session) => {
            var (ev, details) = await eventService.UpdateEvent(singleUpdateDto, session);

            if (updateDto.StartTime != null) {
                // update detachedInstances
                var filter = Builders<DetachedInstances>.Filter.And(
                    Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(updateDto.MasterEventId)),
                    Builders<DetachedInstances>.Filter.Eq(di => di.Id, detachedInstances.Id),
                    Builders<DetachedInstances>.Filter.ElemMatch(di => di.Instances, i => i.EventId == new ObjectId(updateDto.InstanceEventId))
                );
                var update = Builders<DetachedInstances>.Update.Set("instances.$.startTime", updateDto.StartTime);

                await dbService.UpdateOneAsync(detachedInstancesCollection, filter, update, session);
            }

            return (ev, details);
        });

        return new RetrieveEventResponseDto(ev, details: details);
    }
    
    #endregion

    #region sequence
    public async Task<RetrieveRecurrentEventResponseDto> UpdateRecurrentEvent(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        return updateDto.UpdateType switch {
            RecurrentUpdateType.AllTheSequence => await UpdateAllTheSequence(updateDto, profile),
            RecurrentUpdateType.ThisAndAllFollowing => await UpdateThisAndAllFollowing(updateDto, profile),
            _ => throw new InvalidOperationException("Mismatch between the request and the update type"),
        };
    }

    // to check:
    // profileEvents
    private async Task<RetrieveRecurrentEventResponseDto> UpdateAllTheSequence(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        var updates = GetUpdates(updateDto);

        var (upatedEvent, details) = await dbService.ExecuteInTransactionAsync(async (session) => {
            RecurrentEvent? master;
            EventDetails? eventDetails = null;

            if (updates.Count >= 0) {
                var combinedUpdate = Builders<RecurrentEvent>.Update.Combine(updates);
                master = await dbService.FindOneByIdAndUpdateAsync(recurrentEventCollection, new ObjectId(updateDto.MasterEventId), combinedUpdate, session);

                var propagationMessage = new QueueMessage<RecurrentEventPayload>(MessageType.eventUpdate, new(master, EventUpdateType.update));
                await messageService.SendPropagationMessageAsync(propagationMessage);
            }
            else
                master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

            if (updateDto.Description != null)
                eventDetails = await eventDetailsService.Update(new ObjectId(updateDto.MasterEventId), updateDto.Description, session);

            // update current instance
            if (!updateDto.InstanceEventId.Contains(updateDto.MasterEventId)) {
                await UpdateDetachedInstance(updateDto, profile);
            }
            return (master, eventDetails);
        });
        return new RetrieveRecurrentEventResponseDto(upatedEvent, details: details);
    }

    private static List<UpdateDefinition<RecurrentEvent>> GetUpdates(UpdateRecurrentEventRequestDto updateDto) {
        var updates = new List<UpdateDefinition<RecurrentEvent>>();

        if (updateDto.Title != null)
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.Title, updateDto.Title));

        if (updateDto.StartTime != null)
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.StartTime, updateDto.StartTime));

        if (updateDto.EndTime != null)
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.EndTime, updateDto.EndTime));

        if (updateDto.RecurrenceRule != null)
            updates.Add(Builders<RecurrentEvent>.Update.Set(e => e.RecurrenceRule, RecurrenceService.GetValidRule(updateDto.RecurrenceRule)));

        return updates;
    }

    private async Task<RetrieveRecurrentEventResponseDto> UpdateThisAndAllFollowing(UpdateRecurrentEventRequestDto updateDto, Profile profile) {

        var oldMaster = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);
        var stopTime = await GetStopTime(updateDto, oldMaster);

        RetrieveRecurrentEventResponseDto eventDto = await dbService.ExecuteInTransactionAsync(async (session) => {
            // stop previous masterevent
            await UpdateOldMaster(stopTime, updateDto, oldMaster, session);

            var (newMaster, eventDetails, profileRecurrentEvent) = await CreateNewMaster(updateDto, profile, oldMaster, session);

            await UpdateDetachedInstances(oldMaster, newMaster, stopTime, updateDto, session);

            return new RetrieveRecurrentEventResponseDto(
                newMaster,
                eventDetails,
                profileEventDtos: [
                    new ProfileEventDto
                    {
                        ProfileId = profileRecurrentEvent.ProfileId.ToString(),
                        Role = profileRecurrentEvent.Role,
                        Confirmed = profileRecurrentEvent.Confirmed,
                        Trusted = false
                    }
                ]
            );
        });
        return eventDto;
    }

    private async Task<DateTimeOffset> GetStopTime(UpdateRecurrentEventRequestDto updateDto, RecurrentEvent oldMaster) {
        DateTimeOffset stopTime;
        if (updateDto.StartTime != null)
            stopTime = updateDto.StartTime.Value.ToUniversalTime();
        else if (updateDto.InstanceEventId.Contains(updateDto.MasterEventId)) // generated
            stopTime = RecurrenceService.ParseInstanceId(updateDto.InstanceEventId, oldMaster.TimeZone);
        else {  // detached
            var det = await dbService.RetrieveByIdAsync<Event>(CollectionName.Events, updateDto.InstanceEventId);
            stopTime = det.StartTime;
        }
        return stopTime.AddSeconds(-1);
    }

    private async Task UpdateOldMaster(DateTimeOffset stopTime, UpdateRecurrentEventRequestDto updateDto, RecurrentEvent oldMaster, IClientSessionHandle session) {
        var newRecurrenceEnd = stopTime;
        var truncatedRule = RecurrenceService.TruncateRuleUntil(oldMaster.RecurrenceRule, stopTime, oldMaster.TimeZone);
        var oldMasterUpdates = new List<UpdateDefinition<RecurrentEvent>> {
            Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceRule, truncatedRule),
            Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceEnd, newRecurrenceEnd)
        };

        await dbService.UpdateOneByIdAsync(recurrentEventCollection, oldMaster.Id, Builders<RecurrentEvent>.Update.Combine(oldMasterUpdates), session);
    }

    private async Task<(RecurrentEvent, EventDetails, ProfileRecurrentEvent)> CreateNewMaster(UpdateRecurrentEventRequestDto updateDto, Profile profile, RecurrentEvent oldMaster, IClientSessionHandle session) {
        var description = updateDto.Description ?? (await dbService.RetrieveAsync(
           CollectionName.EventDetails,
           Builders<EventDetails>.Filter.Eq(d => d.EventId, oldMaster.Id)))
       .Description;

        var newNewRecurrenceRule = updateDto.RecurrenceRule ?? oldMaster.RecurrenceRule;
        var newMaster = new RecurrentEvent(
            updateDto.Title ?? oldMaster.Title,
            updateDto.StartTime ?? oldMaster.StartTime,
            updateDto.EndTime ?? oldMaster.EndTime,
            oldMaster.TimeZone,
            newNewRecurrenceRule);
        return await recurrentEventService.CreateRecurrentEvent(newMaster, profile, [], description, session);
    }

    private async Task UpdateDetachedInstances(RecurrentEvent oldMaster, RecurrentEvent newMaster, DateTimeOffset stopTime, UpdateRecurrentEventRequestDto updateDto, IClientSessionHandle session) {
        var oldDetachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMaster.Id));
        var allOldInstances = oldDetachedInstances?.Instances ?? [];

        // update old
        if (oldDetachedInstances != null) {
            var stayWithOldMaster = allOldInstances.Where(i => i.StartTime <= stopTime).ToHashSet();

            var detachedInstancesFilter = Builders<DetachedInstances>.Filter.And(
                Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMaster.Id),
                Builders<DetachedInstances>.Filter.Eq(di => di.Id, oldDetachedInstances.Id)
            );
            await dbService.UpdateOneAsync(detachedInstancesCollection, detachedInstancesFilter, Builders<DetachedInstances>.Update.Set(x => x.Instances, stayWithOldMaster), session);
        }

        var migrateToNewMaster = allOldInstances.Where(i => i.StartTime > stopTime).ToHashSet();
        if (migrateToNewMaster.Count > 0) {
            var remappedInstances = new HashSet<DetachedInstance>();

            //change masterId and RecurrencyId to all detached events
            foreach (var instance in migrateToNewMaster) {
                var newRecurrencyId = await UpdateMasterIdAndRecurrencyId(instance, oldMaster, newMaster, updateDto, session);
                remappedInstances.Add(new DetachedInstance(instance.EventId, newRecurrencyId, instance.StartTime));
            }

            var instances = new DetachedInstances(new ObjectId(updateDto.MasterEventId), remappedInstances);
            await dbService.CreateOneAsync(detachedInstancesCollection, instances, session: null);
        }
    }

    private async Task<string> UpdateMasterIdAndRecurrencyId(DetachedInstance detachedInstance, RecurrentEvent oldMaster, RecurrentEvent newMaster, UpdateRecurrentEventRequestDto updateDto, IClientSessionHandle session) {
        var singleEventDuration = new Duration((int)(newMaster.EndTime - newMaster.StartTime).TotalMinutes);

        var originalOccurrenceStartTime = RecurrenceService.ParseInstanceId(detachedInstance.RecurrencyId, oldMaster.TimeZone);

        string newRecurrencyId = RecurrenceService.FindCorrespondingInstanceId(
            newMaster.RecurrenceRule,
            newMaster.StartTime,
            newMaster.RecurrenceEnd,
            newMaster.TimeZone,
            singleEventDuration,
            originalOccurrenceStartTime)
        ?? detachedInstance.RecurrencyId;

        List<UpdateDefinition<Event>> detachedUpdates = [
            Builders<Event>.Update.Set(e => e.RecurrencyInstanceId, newRecurrencyId),
            Builders<Event>.Update.Set(e => e.MasterEventId, newMaster.Id)
        ];

        // update current instance if detached
        // TODO profileEvents?
        if (updateDto.InstanceEventId.Equals(detachedInstance.EventId)) {
            detachedUpdates.AddRange(GetInstanceUpdates(updateDto));

            // details
            await dbService.UpdateOneAsync(CollectionName.EventDetails, Builders<EventDetails>.Filter.Eq(d => d.EventId, detachedInstance.EventId), Builders<EventDetails>.Update.Set(d => d.Description, updateDto.Description ?? ""), session);
        }

        // TODO check Mask? notifications? 

        await dbService.UpdateOneByIdAsync(CollectionName.Events, detachedInstance.EventId, Builders<Event>.Update.Combine(detachedUpdates), session);
        return newRecurrencyId;
    }

    private static List<UpdateDefinition<Event>> GetInstanceUpdates(UpdateRecurrentEventRequestDto updateDto) {
        var updates = new List<UpdateDefinition<Event>>();

        if (updateDto.Title != null)
            updates.Add(Builders<Event>.Update.Set(e => e.Title, updateDto.Title));

        if (updateDto.StartTime != null)
            updates.Add(Builders<Event>.Update.Set(e => e.StartTime, updateDto.StartTime));

        if (updateDto.EndTime != null)
            updates.Add(Builders<Event>.Update.Set(e => e.EndTime, updateDto.EndTime));

        return updates;
    }

    #endregion
}
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

        if (updateDto.IsGeneratedInstance())
            return await CreateDetachedInstance(updateDto, profile);
        else
            return await UpdateDetachedInstance(updateDto, profile);
    }

    private async Task<RetrieveEventResponseDto> CreateDetachedInstance(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        var master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

        var newInstance = GenerateEvent(master, updateDto);

        // check master's DetachedInstances   
        var detachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, master.Id));
        if (detachedInstances != null && detachedInstances.Instances.Any(i => i.RecurrencyId == updateDto.InstanceId))
            throw new ArgumentException("There already exists a detached instance for this slot");

        var (newDetachedInstance, details, profileEvent) = await dbService.ExecuteInTransactionAsync(async (session) => {
            var (newDetachedInstance, details, profileEvent) = await eventService.CreateEvent(newInstance, profile, [], updateDto.Description, session);

            await AddToDetachedInstancesAsync(newDetachedInstance, detachedInstances, updateDto, session);

            return (newDetachedInstance, details, profileEvent);
        });

        return new RetrieveEventResponseDto(newDetachedInstance, details: details, profileEvents: [profileEvent]);
    }

    private static Event GenerateEvent(RecurrentEvent master, UpdateRecurrentEventRequestDto updateDto) {
        // Validate the instanceId and extract the date part
        var datePart = RecurrenceService.CheckRecurrencyId(master.RecurrenceRule, master.StartTime, master.EndTime, master.RecurrenceEnd, master.TimeZone, updateDto.InstanceId);

        var (finalStart, finalEnd) = CalculateTimes(datePart, master, updateDto);

        return new Event(updateDto.Title ?? master.Title, finalStart, finalEnd) {
            RecurrencyInstanceId = updateDto.InstanceId,
            MasterEventId = master.Id,
            DetachedInstance = true,
        };
    }

    private static (DateTimeOffset, DateTimeOffset) CalculateTimes(string datePart, RecurrentEvent master, UpdateRecurrentEventRequestDto updateDto) {
        DateTimeOffset originalOccurrenceStart = RecurrenceService.ParseInstanceId(datePart, master.TimeZone);
        TimeSpan masterDuration = master.EndTime - master.StartTime;
        DateTimeOffset originalOccurrenceEnd = originalOccurrenceStart.Add(masterDuration);

        DateTimeOffset finalStart = updateDto.StartTime ?? originalOccurrenceStart;
        DateTimeOffset finalEnd = updateDto.EndTime ?? originalOccurrenceEnd;

        if (updateDto.StartTime.HasValue && !updateDto.EndTime.HasValue)
            finalEnd = finalStart.Add(masterDuration);

        if (finalEnd <= finalStart)
            throw new ArgumentException("End time must be after start time.");

        return (finalStart, finalEnd);
    }

    private async Task AddToDetachedInstancesAsync(Event newDetachedInstance, DetachedInstances? detachedInstances, UpdateRecurrentEventRequestDto updateDto, IClientSessionHandle session) {
        var singleDetachedInstance = new DetachedInstance(newDetachedInstance.Id, newDetachedInstance.RecurrencyInstanceId!, newDetachedInstance.StartTime);

        if (detachedInstances == null) {
            var instances = new DetachedInstances(new ObjectId(updateDto.MasterEventId), [singleDetachedInstance]);
            await dbService.CreateOneAsync(detachedInstancesCollection, instances, session: session);
        }
        else {
            var update = Builders<DetachedInstances>.Update.AddToSet(x => x.Instances, singleDetachedInstance);
            await dbService.UpdateOneByIdAsync(detachedInstancesCollection, detachedInstances.Id, update, session: session);
        }
    }

    private async Task<RetrieveEventResponseDto> UpdateDetachedInstance(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        // check master's DetachedInstances   
        var detachedInstances = await dbService.RetrieveOrNullAsync(detachedInstancesCollection, Builders<DetachedInstances>.Filter.Eq(list => list.MasterId, new ObjectId(updateDto.MasterEventId)));
        if (detachedInstances == null || !detachedInstances.Instances.Any(i => i.EventId == new ObjectId(updateDto.InstanceId)))
            throw new ArgumentException("This detached instance was not found for the given master");

        var singleUpdateDto = new UpdateEventRequestDto() {
            EventId = updateDto.InstanceId,
            Title = updateDto.Title,
            Description = updateDto.Description,
            StartTime = updateDto.StartTime,
            EndTime = updateDto.StartTime
        };

        var (ev, details) = await dbService.ExecuteInTransactionAsync(async (session) => {
            var (ev, details) = await eventService.UpdateEvent(singleUpdateDto, session);

            if (updateDto.StartTime != null)
                await PropagateToDetachedInstances(updateDto, detachedInstances, session);

            return (ev, details);
        });

        return new RetrieveEventResponseDto(ev, details: details);
    }

    private async Task PropagateToDetachedInstances(UpdateRecurrentEventRequestDto updateDto, DetachedInstances detachedInstances, IClientSessionHandle session) {
        var filter = Builders<DetachedInstances>.Filter.And(
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(updateDto.MasterEventId)),
            Builders<DetachedInstances>.Filter.Eq(di => di.Id, detachedInstances.Id),
            Builders<DetachedInstances>.Filter.ElemMatch(di => di.Instances, i => i.EventId == new ObjectId(updateDto.InstanceId))
        );
        var update = Builders<DetachedInstances>.Update.Set("instances.$.startTime", updateDto.StartTime);

        await dbService.UpdateOneAsync(detachedInstancesCollection, filter, update, session);
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

    #region allsequence
    // to check:
    // profileEvents
    // recurrenceid?
    private async Task<RetrieveRecurrentEventResponseDto> UpdateAllTheSequence(UpdateRecurrentEventRequestDto updateDto, Profile profile) {
        var updates = GetUpdates(updateDto);

        var (upatedEvent, details) = await dbService.ExecuteInTransactionAsync(async (session) => {
            RecurrentEvent? master;
            EventDetails? eventDetails = null;

            // eventually fail BEFORE applying to the whole sequence
            if (!updateDto.IsGeneratedInstance())
                await UpdateDetachedInstance(updateDto, profile);

            if (updates.Count >= 0) {
                master = await dbService.FindOneByIdAndUpdateAsync(recurrentEventCollection, new ObjectId(updateDto.MasterEventId), Builders<RecurrentEvent>.Update.Combine(updates), session);

                var propagationMessage = new QueueMessage<RecurrentEventPayload>(MessageType.eventUpdate, new(master, EventUpdateType.update));
                await messageService.SendPropagationMessageAsync(propagationMessage);
            }
            else
                master = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);

            if (updateDto.Description != null)
                eventDetails = await eventDetailsService.Update(new ObjectId(updateDto.MasterEventId), updateDto.Description, session);

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

    #endregion

    #region thisandfollowing
    private async Task<RetrieveRecurrentEventResponseDto> UpdateThisAndAllFollowing(UpdateRecurrentEventRequestDto updateDto, Profile profile) {

        var oldMaster = await dbService.RetrieveByIdAsync<RecurrentEvent>(recurrentEventCollection, updateDto.MasterEventId);
        var stopTime = await GetStopTime(updateDto, oldMaster);

        RetrieveRecurrentEventResponseDto eventDto = await dbService.ExecuteInTransactionAsync(async (session) => {
            await StopPreviousMaster(stopTime, updateDto, oldMaster, session);

            var (newMaster, eventDetails, profileRecurrentEvent) = await CreateNewMaster(stopTime, updateDto, profile, oldMaster, session);

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
        else if (updateDto.IsGeneratedInstance())
            stopTime = RecurrenceService.ParseInstanceId(updateDto.InstanceId, oldMaster.TimeZone);
        else {  // detached
            var det = await dbService.RetrieveByIdAsync<Event>(CollectionName.Events, updateDto.InstanceId);
            stopTime = det.StartTime;
        }
        return stopTime.AddSeconds(-2);
    }

    private async Task StopPreviousMaster(DateTimeOffset stopTime, UpdateRecurrentEventRequestDto updateDto, RecurrentEvent oldMaster, IClientSessionHandle session) {
        var newRecurrenceEnd = stopTime;
        var truncatedRule = RecurrenceService.TruncateRuleUntil(oldMaster.RecurrenceRule, stopTime);
        var oldMasterUpdates = new List<UpdateDefinition<RecurrentEvent>> {
            Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceRule, truncatedRule),
            Builders<RecurrentEvent>.Update.Set(m => m.RecurrenceEnd, newRecurrenceEnd)
        };

        await dbService.UpdateOneByIdAsync(recurrentEventCollection, oldMaster.Id, Builders<RecurrentEvent>.Update.Combine(oldMasterUpdates), session);
    }

    private async Task<(RecurrentEvent, EventDetails, ProfileRecurrentEvent)> CreateNewMaster(DateTimeOffset stopTime, UpdateRecurrentEventRequestDto updateDto, Profile profile, RecurrentEvent oldMaster, IClientSessionHandle session) {
        var description = updateDto.Description ?? (await dbService.RetrieveAsync(
           CollectionName.EventDetails,
           Builders<EventDetails>.Filter.Eq(d => d.EventId, oldMaster.Id)))
       .Description;


        var newRecurrenceRule = BuildNewMasterRule(updateDto.RecurrenceRule, oldMaster, stopTime);

        var newStartTime = updateDto.StartTime ?? stopTime.AddSeconds(2);
        var newEndTime = updateDto.EndTime ?? newStartTime.Add(oldMaster.GetTimeSpan());

        var newMaster = new RecurrentEvent(
            updateDto.Title ?? oldMaster.Title,
            newStartTime,
            newEndTime,
            oldMaster.TimeZone,
            newRecurrenceRule);
        return await recurrentEventService.CreateRecurrentEvent(newMaster, profile, [], description, session);
    }

    private static string BuildNewMasterRule(string? overrideRule, RecurrentEvent oldMaster, DateTimeOffset splitOccurrenceTime) {
        if (overrideRule != null)
            return overrideRule;

        var oldRule = oldMaster.RecurrenceRule;
        var parts = oldRule.Split(';');
        var countPart = parts.FirstOrDefault(p => p.StartsWith("COUNT=", StringComparison.OrdinalIgnoreCase));

        // No COUNT clause → UNTIL or infinite; the rule is correct as-is for the new series.
        if (countPart == null)
            return oldRule;

        var totalCount = int.Parse(countPart["COUNT=".Length..]);
        var eventDuration = Duration.FromTimeSpanExact(oldMaster.GetTimeSpan());

        // Count occurrences that stay with the OLD master (strictly before the split).
        var occurrencesBefore = RecurrenceService.GetOccurrences(
            oldRule,
            oldMaster.StartTime,
            recurrenceEnd: null,                          // COUNT already limits the series
            oldMaster.TimeZone,
            eventDuration,
            windowStart: oldMaster.StartTime,
            windowEnd: splitOccurrenceTime.AddTicks(-1) // exclusive of the split occurrence
        ).Count();

        var remainingCount = Math.Max(1, totalCount - occurrencesBefore);

        return string.Join(";", parts
            .Where(p => !p.StartsWith("COUNT=", StringComparison.OrdinalIgnoreCase))
            .Append($"COUNT={remainingCount}"));
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
        if (updateDto.InstanceId.Equals(detachedInstance.EventId)) {
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

    #endregion
}
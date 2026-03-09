using Core.Components.Database;
using Core.DTO.MaskAPI;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Model.Masks;
using Core.Model.QueueMessages;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Masks;

public class EventMaskService(
    MongoDbService dbService
)
{
    private readonly CollectionName maskCollection = CollectionName.Masks;
    private readonly CollectionName recurrentMaskCollection = CollectionName.RecurrentMasks;

    #region instances
    public async Task<RetrieveMaskResponseDto> RetrieveEventMask(string eventId, string profileId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<Mask>.Filter.Eq(m => m.EventId, new ObjectId(eventId))
        );

        var mask = await dbService.RetrieveAsync(maskCollection, filter);

        return new RetrieveMaskResponseDto(mask);
    }

    public async Task CreateEventMaks(string profileId, Event ev, IClientSessionHandle session)
    {
        Mask mask = new(new ObjectId(profileId), ev);
        await dbService.CreateOneAsync(maskCollection, mask, session);
    }

    public async Task PropagateEventUpdateAsync(Event ev, EventUpdateType type, IEnumerable<ObjectId> profileIds, string? actorId = null)
    {
        switch (type)
        {
            case EventUpdateType.create:
                { // creator is the only confirmed profileEvent at creation time 
                    var creatorId = new ObjectId(actorId);
                    if (creatorId != default)
                        await CreateOrOverwriteMaskAsync(creatorId, ev);
                    break;
                }
            case EventUpdateType.confirm:
                {
                    if (actorId != null)
                        await CreateOrOverwriteMaskAsync(new ObjectId(actorId), ev);
                    break;
                }
            case EventUpdateType.update:
                {
                    foreach (var pid in profileIds)
                        await CreateOrOverwriteMaskAsync(pid, ev);
                    break;
                }
            case EventUpdateType.decline:
                {
                    if (actorId != null)
                        await DeleteEventMaskAsync(new ObjectId(actorId), ev.Id);
                    break;
                }
            case EventUpdateType.share: // no mask creation on share 
                break;
        }
    }

    private async Task CreateOrOverwriteMaskAsync(ObjectId profileId, Event ev)
    {

        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<Mask>.Filter.Eq(m => m.EventId, ev.Id)
            );

        var update = Builders<Mask>.Update
            .SetOnInsert(m => m.ProfileId, profileId)
            .SetOnInsert(m => m.EventId, ev.Id)
            .Set(m => m.StartTime, ev.StartTime)
            .Set(m => m.EndTime, ev.EndTime)
            .Set(m => m.Title, ev.Title);

        var options = new UpdateOptions<Mask> { IsUpsert = true };


        await dbService.UpdateOneAsync(maskCollection, filter, update, session: null, options: options);
    }

    private async Task DeleteEventMaskAsync(ObjectId profileId, ObjectId eventId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<Mask>.Filter.Eq(m => m.EventId, eventId)
            );

        await dbService.GetCollection<Mask>(maskCollection).DeleteOneAsync(filter);
    }
    #endregion

    #region recurrence
    /*
    public async Task<RetrieveMaskResponseDto> RetrieveRecurrentEventMask(string eventId, string profileId)
    {
        var filter = Builders<RecurrentMask>.Filter.And(
            Builders<RecurrentMask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<RecurrentMask>.Filter.Eq(m => m.EventId, new ObjectId(eventId))
        );

        var mask = await dbService.RetrieveAsync(recurrentMaskCollection, filter);

        return new RetrieveMaskResponseDto(mask);
    }*/

    public async Task CreateRecurrentEventMaks(string profileId, RecurrentEvent ev, IClientSessionHandle session)
    {
        RecurrentMask mask = new(new ObjectId(profileId), ev);
        await dbService.CreateOneAsync(maskCollection, mask, session);
    }

    public async Task PropagateRecurrentEventUpdateAsync(RecurrentEvent ev, EventUpdateType type, IEnumerable<ObjectId> profileIds, string? actorId = null)
    {
        switch (type)
        {
            case EventUpdateType.create:
                {
                    var creatorId = new ObjectId(actorId);
                    if (creatorId != default)
                        await CreateRecurrentMaskAsync(creatorId, ev);
                    break;
                }
            case EventUpdateType.confirm:
                {
                    break;
                }
            case EventUpdateType.update:
                {
                    foreach (var pid in profileIds)
                        await UpdateRecurrentMaskAsync(pid, ev);
                    break;
                }
            case EventUpdateType.decline:
                {
                    if (actorId != null)
                        await DeleteRecurrentEventMaskAsync(new ObjectId(actorId), ev.Id);
                    break;
                }
            case EventUpdateType.share: // no mask creation on share 
                break;
        }
    }

    private async Task CreateRecurrentMaskAsync(ObjectId profileId, RecurrentEvent ev)
    {
        var mask = new RecurrentMask(
            profileId,
            ev
        );

        await dbService.CreateOneAsync(recurrentMaskCollection, mask, null);
    }

    private async Task UpdateRecurrentMaskAsync(ObjectId profileId, RecurrentEvent ev)
    {

        var filter = Builders<RecurrentMask>.Filter.And(
            Builders<RecurrentMask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<RecurrentMask>.Filter.Eq(m => m.EventId, ev.Id)
            );

        var update = Builders<RecurrentMask>.Update
            .SetOnInsert(m => m.ProfileId, profileId)
            .SetOnInsert(m => m.EventId, ev.Id)
            .Set(m => m.StartTime, ev.StartTime)
            .Set(m => m.EndTime, ev.EndTime)
            .Set(m => m.Title, ev.Title)
            .Set(m => m.RecurrenceEnd, ev.RecurrenceEnd)
            .Set(m => m.TimeZone, ev.TimeZone)
            .Set(m => m.RecurrenceRule, ev.RecurrenceRule);

        var options = new UpdateOptions<RecurrentMask> { IsUpsert = true };
        // TODO update all instances
        await dbService.UpdateOneAsync(recurrentMaskCollection, filter, update, session: null, options: options);
    }

    private async Task DeleteRecurrentEventMaskAsync(ObjectId profileId, ObjectId eventId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<Mask>.Filter.Eq(m => m.EventId, eventId)
            );
        // TODO delete all instances
        await dbService.GetCollection<Mask>(CollectionName.Masks).DeleteOneAsync(filter);
    }
    #endregion
}
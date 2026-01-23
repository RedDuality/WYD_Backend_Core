using Core.Components.Database;
using Core.DTO.MaskAPI;
using Core.Model.Events;
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
                { // creator is the only profileEvent at creation time 
                    var creatorId = profileIds.FirstOrDefault();
                    if (creatorId != default)
                        await UpsertMaskAsync(creatorId, ev);
                    break;
                }
            case EventUpdateType.confirm:
                {
                    if (actorId != null)
                        await UpsertMaskAsync(new ObjectId(actorId), ev);
                    break;
                }
            case EventUpdateType.update:
                {
                    foreach (var pid in profileIds)
                        await UpsertMaskAsync(pid, ev);
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

    private async Task UpsertMaskAsync(ObjectId profileId, Event ev)
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

        await dbService.UpdateOneAsync(CollectionName.Masks, filter, update, session: null, options: options);
    }

    private async Task DeleteEventMaskAsync(ObjectId profileId, ObjectId eventId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<Mask>.Filter.Eq(m => m.EventId, eventId)
            );

        await dbService.GetCollection<Mask>(CollectionName.Masks).DeleteOneAsync(filter);
    }

}
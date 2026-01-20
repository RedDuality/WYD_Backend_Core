using Core.Components.Database;
using Core.DTO.MaskAPI;
using Core.Model.Events;
using Core.Model.Masks;
using Core.Model.QueueMessages;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Masks;

public class MaskService(MongoDbService dbService
)
{
    private readonly CollectionName maskCollection = CollectionName.Masks;

    public async Task<RetrieveMaskResponseDto> CreateMaskAsync(string profileId, CreateMaskRequestDto createDto)
    {
        Mask mask = new(new ObjectId(profileId), createDto.StartTime, createDto.EndTime, createDto.Title);
        await dbService.CreateOneAsync(maskCollection, mask, null);
        return new RetrieveMaskResponseDto(mask);
    }

    public async Task CreateEventMaks(string profileId, Event ev, IClientSessionHandle session)
    {
        Mask mask = new(new ObjectId(profileId), ev);
        await dbService.CreateOneAsync(maskCollection, mask, session);
    }

    public async Task<RetrieveMaskResponseDto> UpdateMaskAsync(UpdateMaskRequestDto updateDto)
    {
        var updates = GetUpdates(updateDto);

        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.Id, new ObjectId(updateDto.MaskId)),
            Builders<Mask>.Filter.Eq(m => m.EventId, null) // Cannot update if EventId is set
        );

        try
        {
            var updatedMask = await dbService.FindOneAndUpdateAsync(
                maskCollection,
                filter,
                updates
            );

            return new RetrieveMaskResponseDto(updatedMask);
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException($"Update failed. Mask '{updateDto.MaskId}' either does not exist or is linked to an event and cannot be modified directly.");
        }
    }

    private static UpdateDefinition<Mask> GetUpdates(UpdateMaskRequestDto updateDto)
    {
        var updates = new List<UpdateDefinition<Mask>>
        {
            Builders<Mask>.Update.Set(e => e.Title, updateDto.Title)
        };

        if (updateDto.StartTime != null)
            updates.Add(Builders<Mask>.Update.Set(e => e.StartTime, updateDto.StartTime));

        if (updateDto.EndTime != null)
            updates.Add(Builders<Mask>.Update.Set(e => e.EndTime, updateDto.EndTime));

        return Builders<Mask>.Update.Combine(updates); ;
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


    public async Task DeleteMaskAsync(ObjectId profileId, ObjectId maskId)
    {
        // no index as too storage expensive as it should not happen a lot
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, profileId),
            Builders<Mask>.Filter.Eq(m => m.Id, maskId)
        );

        await dbService.GetCollection<Mask>(CollectionName.Masks).DeleteOneAsync(filter);
    }

    public async Task<List<RetrieveMaskResponseDto>> RetrieveMasks(RetrieveMultipleMaskRequestDto retrieveDto)
    {
        var filterBuilder = Builders<Mask>.Filter;
        var profileObjectIds = retrieveDto.ProfileIds.Select(id => ObjectId.Parse(id));

        var filter = filterBuilder.And(
            filterBuilder.In(m => m.ProfileId, profileObjectIds),
            filterBuilder.Gte(m => m.EndTime, retrieveDto.StartTime.ToUniversalTime()),
            filterBuilder.Lte(m => m.StartTime, retrieveDto.EndTime.ToUniversalTime())
        );


        var masks = await dbService.RetrieveMultipleAsync(maskCollection, filter);

        return [.. masks.Select(m => new RetrieveMaskResponseDto(m))];
    }

    public async Task<RetrieveMaskResponseDto> RetrieveEventMask(string eventId, string profileId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<Mask>.Filter.Eq(m => m.EventId, new ObjectId(eventId))
        );

        var mask = await dbService.RetrieveAsync(maskCollection, filter);

        return new RetrieveMaskResponseDto(mask);
    }



}
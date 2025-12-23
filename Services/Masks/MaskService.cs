using Core.Components.Database;
using Core.DTO.MaskAPI;
using Core.Model.Events;
using Core.Model.Masks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Masks;

public class MaskService(MongoDbService dbService
)
{
    private readonly CollectionName maskCollection = CollectionName.Masks;

    public async Task<RetrieveMaskResponseDto> CreateMask(string profileId, CreateMaskRequestDto createDto)
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

}
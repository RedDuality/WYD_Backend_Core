using Core.Components.Database;
using MongoDB.Driver;
using Core.DTO.ProfileAPI;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using Core.Model.Profiles;

namespace Core.Services.Users;

public class ProfileTagService(MongoDbService dbService)
{
    private readonly CollectionName profileTagsCollection = CollectionName.ProfileTags;


    public async Task CreateAsync(Profile profile, IClientSessionHandle? session)
    {
        await dbService.CreateOneAsync(profileTagsCollection, new ProfileTag(profile), session);
    }

    public async Task<List<RetrieveProfileResponseDto>> SearchByTagAsync(string searchTag)
    {
        var filter = Builders<ProfileTag>.Filter.Regex(p => p.Tag, new BsonRegularExpression($"^{Regex.Escape(searchTag)}"));

        var profileTags = await dbService.RetrieveMultipleAsync(profileTagsCollection, filter, limit: 5);
        var profileIds = profileTags.Select(t => t.ProfileId).ToHashSet();

        var profiles = await dbService.RetrieveByIdsAsync<Profile>(CollectionName.Profiles, profileIds);
        var profileDtos = profiles.Select(p => new RetrieveProfileResponseDto(p)).ToList();
        return profileDtos;
    }

    public async Task Update(ObjectId profileId, string newTag, IClientSessionHandle session)
    {
        var filter = Builders<ProfileTag>.Filter.Eq(tag => tag.ProfileId, profileId);
        var updateDefinition = Builders<ProfileTag>.Update.Set(tag => tag.Tag, newTag);
        await dbService.UpdateOneAsync(profileTagsCollection, filter, updateDefinition, session: session);
    }
}

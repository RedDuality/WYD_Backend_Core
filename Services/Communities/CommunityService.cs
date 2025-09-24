using Core.Components.Database;
using Core.DTO.CommunityAPI;
using Core.Model.Communities;
using Core.Model.Profiles;
using Core.Services.Users;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class CommunityService(MongoDbService dbService, ProfileService profileService, GroupService groupService, ProfileCommunityService profileCommunityService)
{
    private readonly CollectionName communityCollection = CollectionName.Communities;

    public async Task<RetrieveCommunityResponseDto> Create(CreateCommunityRequestDto dto, Profile ownerProfile)
    {
        HashSet<Profile> profiles = [];
        if (dto.ProfileIds.Count > 0)
            profiles = await profileService.RetrieveMultipleProfileById([.. dto.ProfileIds]);
        profiles.Add(ownerProfile);

        var community = new Community(dto.Name, ownerProfile, dto.Type);

        RetrieveCommunityResponseDto communityDto = await dbService.ExecuteInTransactionAsync(async (session) =>
            {
                await dbService.CreateOneAsync(communityCollection, community, session);
                var group = await AddGroup(community, profiles, ownerProfile, dto.Name, true, session);
                var profileCommunities = await profileCommunityService.CreateAsync(community, group, ownerProfile, profiles);
                return profileCommunities
                    .Where(pc => pc.ProfileId == ownerProfile.Id)
                    .Select(pc => new RetrieveCommunityResponseDto(pc))
                    .First();
            });
        return communityDto;
    }

    public async Task<Group> AddGroup(
        Community community,
        HashSet<Profile> profiles,
        Profile ownerProfile,
        string? name = null,
        bool? mainGroup = null,
        IClientSessionHandle? session = null)
    {
        var group = await groupService.CreateAsync(profiles, ownerProfile, community, name, mainGroup, session);

        var updates = new List<UpdateDefinition<Community>>();

        if (mainGroup == true)
            updates.Add(Builders<Community>.Update.Set(c => c.MainGroupId, group.Id));

        updates.Add(Builders<Community>.Update.AddToSet(c => c.Groups, group.Id));

        await dbService.UpdateOneByIdAsync(communityCollection, community.Id, Builders<Community>.Update.Combine(updates), session);
        return group;
    }

    public HashSet<RetrieveCommunityResponseDto> Retrieve(Profile profile)
    {
    }

    public Community MakeMultiGroup(Community community)
    {
        if (community.Type == CommunityType.Personal)
            throw new Exception("Cannot transform this chat into a community");

        community.Type = CommunityType.Community;
        return community;
    }

}
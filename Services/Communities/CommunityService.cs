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
            profiles = await profileService.RetrieveMultiple([.. dto.ProfileIds]);

        profiles.Add(ownerProfile);

        if (dto.Type == CommunityType.Personal)
        {
            var oldCommunity = await profileCommunityService.FindPersonalCommunity(ownerProfile, profiles.Where(p => p.Id != ownerProfile.Id).First());
            if (oldCommunity != null)
                return new RetrieveCommunityResponseDto(oldCommunity);
        }
        
        var community = new Community(dto.Name, ownerProfile, dto.Type);

        List<ProfileCommunity> profileCommunities = await dbService.ExecuteInTransactionAsync(async (session) =>
            {
                await dbService.CreateOneAsync(communityCollection, community, session);
                var group = await AddGroup(
                    community,
                    profiles,
                    ownerProfile,
                    mainGroup: community.Type != CommunityType.Personal,
                    session: session);

                var profileCommunities = await profileCommunityService.CreateAsync(community, group, ownerProfile, profiles, session);

                return profileCommunities;
            });

        var currentProfileCommunity = profileCommunities.Where(pc => pc.ProfileId == ownerProfile.Id).First();

        return new RetrieveCommunityResponseDto(currentProfileCommunity);
    }

    public async Task<Group> AddGroup(
        Community community,
        HashSet<Profile> profiles,
        Profile ownerProfile,
        bool mainGroup = false,
        string name = "General",
        IClientSessionHandle? session = null)
    {
        var group = await groupService.CreateAsync(profiles, ownerProfile, community, mainGroup, name, session);

        var updates = new List<UpdateDefinition<Community>>();

        if (mainGroup == true)
            updates.Add(Builders<Community>.Update.Set(c => c.MainGroupId, group.Id));

        updates.Add(Builders<Community>.Update.AddToSet(c => c.Groups, group.Id));

        await dbService.UpdateOneByIdAsync(communityCollection, community.Id, Builders<Community>.Update.Combine(updates), session);
        return group;
    }


    public async Task<HashSet<RetrieveCommunityResponseDto>> Retrieve(Profile profile)
    {
        var profileCommunities = await profileCommunityService.RetrieveProfileCommunitiesByProfile(profile);
        var responseDtos = profileCommunities.Select((pc) => new RetrieveCommunityResponseDto(pc)).ToHashSet();
        return responseDtos;
    }

    public Community MakeMultiGroup(Community community)
    {
        if (community.Type == CommunityType.Personal)
            throw new Exception("Cannot transform this chat into a community");

        community.Type = CommunityType.Community;
        return community;
    }

}
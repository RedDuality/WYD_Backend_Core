using Core.Components.Database;
using Core.Components.MessageQueue;
using Core.DTO.CommunityAPI;
using Core.Model.Communities;
using Core.Model.Notifications;
using Core.Model.Profiles;
using Core.Services.Profiles;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class CommunityService(
    MongoDbService dbService,
    ProfileService profileService,
    GroupService groupService,
    ProfileCommunityService profileCommunityService,
    IMessageQueueService messageService)
{
    private readonly CollectionName communityCollection = CollectionName.Communities;

    public async Task<RetrieveCommunityResponseDto> Create(CreateCommunityRequestDto dto, Profile ownerProfile)
    {
        var profiles = await RetrieveProfiles(dto.ProfileIds, ownerProfile);

        // check community does not already exists
        if (dto.Type == CommunityType.Personal)
        {
            var oldCommunity = await profileCommunityService.FindPersonalCommunity(ownerProfile, profiles.First(p => p.Id != ownerProfile.Id));
            if (oldCommunity != null)
                return new RetrieveCommunityResponseDto(oldCommunity);
        }

        var community = new Community(dto.Name, ownerProfile, dto.Type);

        List<ProfileCommunity> profileCommunities = await dbService.ExecuteInTransactionAsync(async (session) =>
            {
                await dbService.CreateOneAsync(communityCollection, community, session);

                var group = await CreateAndAddGroup(
                    community,
                    profiles,
                    ownerProfile,
                    community.Type != CommunityType.Personal ? "Personal" : "General",
                    session,
                    mainGroup: community.Type != CommunityType.Personal
                    );

                var profileCommunities = await profileCommunityService.CreateAsync(community, group, ownerProfile, profiles, session);

                return profileCommunities;
            });

        await SendCreateCommunityNotification(community);

        var currentProfileCommunity = profileCommunities.First(pc => pc.ProfileId == ownerProfile.Id);
        return new RetrieveCommunityResponseDto(currentProfileCommunity);
    }

    private async Task<HashSet<Profile>> RetrieveProfiles(List<string> profileIds, Profile ownerProfile)
    {
        HashSet<Profile> profiles = [];
        if (profileIds.Count > 0)
            profiles = await profileService.RetrieveMultiple([.. profileIds]);

        profiles.Add(ownerProfile);

        return profiles;
    }

    public async Task<Group> CreateAndAddGroup(
        Community community,
        HashSet<Profile> profiles,
        Profile ownerProfile,
        string name,
        IClientSessionHandle session,
        bool mainGroup = false)
    {
        var group = await groupService.CreateAsync(profiles, ownerProfile, community, mainGroup, name, session);

        var updates = GetCommunityUpdates(mainGroup, group);

        await dbService.UpdateOneByIdAsync(communityCollection, community.Id, Builders<Community>.Update.Combine(updates), session);
        return group;
    }

    private static List<UpdateDefinition<Community>> GetCommunityUpdates(bool mainGroup, Group group)
    {
        var updates = new List<UpdateDefinition<Community>>();

        if (mainGroup == true)
            updates.Add(Builders<Community>.Update.Set(c => c.MainGroupId, group.Id));

        updates.Add(Builders<Community>.Update.AddToSet(c => c.Groups, group.Id));

        return updates;
    }

    private async Task SendCreateCommunityNotification(Community community)
    {
        var notification = new Notification(
            community.Id,
            NotificationType.CreateCommunity,
            community.UpdatedAt
        );
        await messageService.SendNotificationAsync(notification);
    }


    public async Task<Community> MakeMultiGroupAsync(Community community)
    {
        if (community.Type == CommunityType.Personal)
            throw new Exception("Cannot transform this chat into a community");

        var update = Builders<Community>.Update.Set(c => c.Type, CommunityType.Community);
        await dbService.UpdateOneByIdAsync(communityCollection, community.Id, update);

        return community;
    }

    public async Task<HashSet<RetrieveCommunityResponseDto>> RetrieveCommunities(Profile profile)
    {
        var profileCommunities = await profileCommunityService.RetrieveProfileCommunitiesByProfile(profile);
        var responseDtos = profileCommunities.Select((pc) => new RetrieveCommunityResponseDto(pc)).ToHashSet();
        return responseDtos;
    }

}
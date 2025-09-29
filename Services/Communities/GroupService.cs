
using Core.Components.Database;
using Core.DTO.CommunityAPI;
using Core.Model.Communities;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class GroupService(MongoDbService dbService)
{
    private readonly CollectionName groupCollection = CollectionName.Groups;

    // use AddGroup from CommunityService
    public async Task<Group> CreateAsync(
        HashSet<Profile> profiles,
        Profile owner,
        Community community,
        bool mainGroup,
        string name,
        IClientSessionHandle? session = null)
    {
        var groupProfiles = profiles.Select((p) =>
            {
                return new GroupProfile(p, p.Id == owner.Id ? GroupRole.Owner : GroupRole.Viewer);
            }).ToHashSet();

        var group = new Group(community, name, groupProfiles, mainGroup);

        await dbService.CreateOneAsync(groupCollection, group, session);
        return group;
    }




    public async Task<HashSet<ObjectId>> GetProfilesByGroupIds(List<ShareEventRequestDto> dtos, Profile currentProfile)
    {
        var groupIds = dtos.Select(dto => new ObjectId(dto.GroupId)).ToList();
        var communityIds = dtos.Select(dto => new ObjectId(dto.CommunityId)).Distinct().ToList();

        var filter = Builders<Group>.Filter.And(
            Builders<Group>.Filter.In("_id", groupIds),
            Builders<Group>.Filter.In("communityId", communityIds)
        );

        var groups = await dbService.RetrieveMultipleAsync(groupCollection, filter);

        var profileIds = new HashSet<ObjectId>();

        foreach (var group in groups)
        {
            bool currentProfileFound = false;

            foreach (var groupProfile in group.Profiles)
            {
                if (groupProfile.ProfileId == currentProfile.Id)
                {
                    currentProfileFound = true;
                    continue; // Skip adding currentProfile to the result
                }

                if (groupProfile.ProfileId.HasValue)
                {
                    profileIds.Add(groupProfile.ProfileId.Value);
                }
            }

            if (!currentProfileFound)
            {
                throw new UnauthorizedAccessException($"Current profile is not a member of group {group.Id}");
            }
        }

        return profileIds;
    }
}
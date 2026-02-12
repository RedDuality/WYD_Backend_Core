
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
        IClientSessionHandle session)
    {
        var groupProfiles = profiles.Select((p) =>
            {
                return new GroupProfile(p, p.Id == owner.Id ? GroupRole.Owner : GroupRole.Viewer);
            }).ToHashSet();

        var group = new Group(community, name, groupProfiles, mainGroup);

        await dbService.CreateOneAsync(groupCollection, group, session);
        return group;
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="groupIdentification"></param>
    /// <param name="currentProfile">current Profile is removed from the result</param>
    /// <returns> A set of ids of the join of the profiles in the grouds inside the identification, withoud including currentProfile</returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public async Task<HashSet<ObjectId>> GetProfilesByGroupIds(List<ShareGroupIdentifierDto> groupIdentification, Profile currentProfile)
    {
        var groupIds = groupIdentification.Select(dto => new ObjectId(dto.GroupId)).ToList();
        var communityIds = groupIdentification.Select(dto => new ObjectId(dto.CommunityId)).Distinct().ToList();

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
                    continue; // Do not add currentProfile to the result
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
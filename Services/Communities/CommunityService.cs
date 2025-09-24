using Core.Components.Database;

namespace Core.Services.Communities;
public class CommunityService(MongoDbService dbService)
{

    public Community Retrieve(int id)
    {
        return RetrieveOrNull(id) ?? throw new KeyNotFoundException("Community");

    }
    public Community Create(CreateCommunityDto dto, Profile profile)
    {
        using var transaction = db.Database.BeginTransaction();
        try
        {
            Community newCommunity = FromDto(dto, profile);

            db.Communities.Add(newCommunity);
            db.SaveChanges();

            Group group = new()
            {
                Name = "General",
                GeneralForCommunity = true,
                Community = newCommunity,
                Profiles = newCommunity.Profiles,
            };

            AddGroup(newCommunity, group);

            transaction.Commit();

            return newCommunity;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Error creating community. Transaction rolled back.", ex);
        }
    }

    public Community CreateAndAddGroup(Community community, GroupDto dto, Profile profile)
    {
        //TODO check profiles are already in the commmunity
        dto.ProfileHashes.Add(profile.Hash);

        Group group = new()
        {
            Name = dto.Name ?? "New Group",
            GeneralForCommunity = false,
            Community = community,
            Profiles = db.Profiles.Where((p) => dto.ProfileHashes.Contains( p.Hash)).ToHashSet(),
        };

        return AddGroup(community, group);
    }

    private Community AddGroup(Community community, Group group)
    {
        community.Groups.Add(group);
        db.SaveChanges();

        return community;
    }

    public Community MakeMultiGroup(Community community)
    {
        if (community.Type == CommunityType.Personal)
            throw new Exception("Cannot transform this chat into a community");

        community.Type = CommunityType.Community;
        db.SaveChanges();
        return community;
    }

}
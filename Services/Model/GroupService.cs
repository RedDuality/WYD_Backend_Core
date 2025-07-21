using Model;
using Database;

namespace Service;
public class GroupService(WydDbContext context)
{
    private readonly WydDbContext db = context ?? throw new ArgumentNullException(nameof(context), "Database context cannot be null");


    public Group? RetrieveOrNull(int groupId)
    {
        return db.Groups.Find(groupId);
    }

    public HashSet<Group> Retrieve(HashSet<int> groupIds)
    {
        return db.Groups.Where(g => groupIds.Contains(g.Id)).ToHashSet();
    }
}
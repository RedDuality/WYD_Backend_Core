using Core.Components.Database;
using Core.Model.Events;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Model.Notifications;

public interface IProfileFinder
{
    Task<List<ObjectId>> GetProfileIdsAsync(MongoDbService dbService, Notification notification);
}


public class EventProfileFinder : IProfileFinder
{
    public async Task<List<ObjectId>> GetProfileIdsAsync(MongoDbService dbService, Notification notification)
    {
        var eventProfiles = await dbService.RetrieveMultipleAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.Where(ep => ep.EventId == new ObjectId(notification.ObjectId))
        );
        return eventProfiles.Select(ep => ep.ProfileId).ToList();
    }
}


/*
public class GroupProfileFinder : IProfileFinder
{
    public async Task<List<ObjectId>> GetProfileIdsAsync(MongoDbService dbService, Notification notification)
    {
        var groupProfiles = await dbService.RetrieveMultipleAsync(
            CollectionName.GroupProfiles,
            Builders<GroupProfile>.Filter.Where(gp => gp.GroupId == new ObjectId(notification.ObjectId))
        );
        return groupProfiles.Select(gp => gp.ProfileId).ToList();
    }
}
*/
using Core.Model.Notifications;
using Core.Services.Communities;
using Core.Services.Events;
using MongoDB.Bson;

namespace Core.Services.Notifications;

public interface IProfileFinder
{
    Task<List<ObjectId>> GetProfileIdsAsync(ObjectId objectId);
}

public class ProfileIdResolverFactory
{
    private readonly Dictionary<NotificationType, IProfileFinder> _resolvers;

    public ProfileIdResolverFactory(
        EventProfileService eventProfileService,
        CommunityProfileService communityProfileService)
    {
        _resolvers = new()
        {
            //{ NotificationType.CreateEvent, eventProfileService },
            //{ NotificationType.ShareEvent, eventProfileService },
            { NotificationType.UpdateEssentialsEvent, eventProfileService },
            { NotificationType.ConfirmEvent, eventProfileService },
            { NotificationType.DeclineEvent, eventProfileService },
            //{ NotificationType.UpdateDetailsEvent, eventProfileService },
            { NotificationType.UpdatePhotos, eventProfileService },

            { NotificationType.DeleteEvent, eventProfileService },
            { NotificationType.DeleteEventForAll, eventProfileService },
            //{ NotificationType.GroupUpdate, new GroupProfileFinder() }
            { NotificationType.CreateCommunity, communityProfileService }

        };
    }

    public IProfileFinder Resolve(NotificationType type)
    {
        if (!_resolvers.TryGetValue(type, out var resolver))
            throw new NotSupportedException($"No profile resolver for type {type}");

        return resolver;
    }
}

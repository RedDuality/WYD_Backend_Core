using Core.Model.Notifications;

public class ProfileIdResolverFactory
{
    private readonly Dictionary<NotificationType, IProfileFinder> _resolvers;

    public ProfileIdResolverFactory()
    {
        _resolvers = new()
        {
            { NotificationType.CreateEvent, new EventProfileFinder() },
            { NotificationType.ShareEvent, new EventProfileFinder() },
            { NotificationType.UpdateEssentialsEvent, new EventProfileFinder() },
            { NotificationType.UpdateDetailsEvent, new EventProfileFinder() },
            { NotificationType.UpdatePhotos, new EventProfileFinder() },
            { NotificationType.ConfirmEvent, new EventProfileFinder() },
            { NotificationType.DeclineEvent, new EventProfileFinder() },
            { NotificationType.DeleteEvent, new EventProfileFinder() },
            { NotificationType.DeleteEventForAll, new EventProfileFinder() },
            //{ NotificationType.GroupUpdate, new GroupProfileFinder() }

        };
    }

    public IProfileFinder Resolve(NotificationType type)
    {
        if (!_resolvers.TryGetValue(type, out var resolver))
            throw new NotSupportedException($"No profile resolver for type {type}");

        return resolver;
    }
}

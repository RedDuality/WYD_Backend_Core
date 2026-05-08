using Microsoft.Extensions.DependencyInjection;
using Moq;
using Core.Services.Events;
using Core.Services.Profiles;
using Core.Services.Communities;
using Core.Services.Notifications;
using Core.Services.Masks;
using Core.Components.Database;
using Core.Components.MessageQueue;
using Core.Services.Users;
using Core.Services.Util;
using Core.Services.Events.Instances;
using Core.Services.Events.Recurrence;
using Core.Components.MessageQueue.ImplementationSpecific;

namespace Core.Tests.Setup;

public static class TestServiceFactory
{
    public static IServiceProvider CreateServiceProvider(MongoDbService dbService)
    {
        var services = new ServiceCollection();

        services.AddSingleton(dbService);


        services.AddScoped<IMessageQueueService, MessageQueueService>();
        services.AddScoped<MessageQueueHandlerService>();

        services.AddScoped<UserService>();
        services.AddScoped<UserProfileService>();
        services.AddScoped<UserClaimService>();

        services.AddScoped<ProfileService>();
        services.AddScoped<ProfileDetailsService>();
        services.AddScoped<ProfileTagService>();
        services.AddScoped<ProfileProfileService>();

        services.AddScoped<ImportedProfilesService>();
        services.AddScoped<ProfileUpdatePropagationService>();

        services.AddScoped<MaskProfileService>();
        services.AddScoped<EventMaskService>();

        services.AddScoped<EventRetrieveService>();
        services.AddScoped<EventUpdatePropagationService>();
        services.AddScoped<EventProfileService>();

        services.AddScoped<RecurrentEventService>();
        services.AddScoped<RecurrentEventUpdateService>();
        services.AddScoped<EventUpdatePropagationService>();
        services.AddScoped<RecurrentEventProfileService>();

        services.AddScoped<EventDetailsService>();
        services.AddScoped<ProfileEventService>();

        services.AddSingleton(_ => new Mock<MediaService>(dbService, null!).Object);

        services.AddScoped<CommunityProfileService>();

        services.AddSingleton(new Mock<IContextManager>().Object);

        services.AddSingleton(_ => new Mock<GroupService>(dbService).Object);


        services.AddSingleton(_ => new Mock<BroadcastService>(null!, null!).Object);

        services.AddScoped<ProfileIdResolverFactory>();


        return services.BuildServiceProvider();
    }
}
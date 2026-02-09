using Microsoft.Extensions.DependencyInjection;
using Moq;
using Core.Services.Events;
using Core.Services.Profiles;
using Core.Services.Communities;
using Core.Services.Notifications;
using Core.Services.Masks;
using Core.Components.Database;
using Core.Components.MessageQueue;

namespace Core.Tests.Setup;

public static class TestServiceFactory
{
    public static IServiceProvider CreateServiceProvider(MongoDbService dbService)
    {
        var services = new ServiceCollection();

        services.AddSingleton(dbService);


        services.AddScoped<MessageQueueService>();

        services.AddScoped<ProfileService>();
        services.AddScoped<ProfileDetailsService>();
        services.AddScoped<ProfileProfileService>();
        services.AddScoped<ProfileUpdatePropagationService>();

        services.AddScoped<MaskProfileService>();
        services.AddScoped<EventMaskService>();

        services.AddScoped<EventService>();
        services.AddScoped<EventUpdatePropagationService>();

        services.AddScoped<EventDetailsService>();
        services.AddScoped<ProfileEventService>();
        services.AddScoped<EventProfileService>();

        services.AddSingleton(_ => new Mock<MediaService>(dbService, null!).Object);

        services.AddScoped<CommunityProfileService>();

        services.AddSingleton(_ => new Mock<GroupService>(dbService).Object);


        services.AddSingleton(_ => new Mock<BroadcastService>(null!, null!).Object);

        services.AddScoped<ProfileIdResolverFactory>();

        services.AddScoped<IMessageQueueHandlerService, MessageQueueHandlerService>();

        return services.BuildServiceProvider();
    }
}
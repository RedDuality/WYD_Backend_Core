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

        // 1. Database & Infrastructure
        services.AddSingleton(dbService);

        // 2. Mock External Dependencies
        var mockMedia = new Mock<MediaService>(dbService, null!);
        var mockGroup = new Mock<GroupService>(dbService);
        var mockBroadcast = new Mock<BroadcastService>(null!, null!);

        services.AddSingleton(mockMedia); // Register the Mock object itself for access in tests
        services.AddSingleton(mockMedia.Object);
        services.AddSingleton(mockGroup.Object);
        services.AddSingleton(mockBroadcast.Object);

        // 3. Domain Services (Directly from Program.cs logic)
        services.AddScoped<EventService>();
        services.AddScoped<EventDetailsService>();
        services.AddScoped<EventProfileService>();
        services.AddScoped<ProfileEventService>();
        services.AddScoped<EventMaskService>();
        services.AddScoped<CommunityProfileService>();
        services.AddScoped<ProfileDetailsService>();
        services.AddScoped<ProfileProfileService>();
        services.AddScoped<MaskProfileService>();
        services.AddScoped<ProfileIdResolverFactory>();
        services.AddScoped<EventUpdatePropagationService>();
        services.AddScoped<ProfileUpdatePropagationService>();
        services.AddScoped<IMessageQueueHandlerService, MessageQueueHandlerService>();
        services.AddScoped<MessageQueueService>();
        services.AddScoped<ProfileService>();
        // Mock Notification components to avoid complex sub-dependency chains
        services.AddScoped(_ => new Mock<BroadcastService>(null!, null!).Object);

        return services.BuildServiceProvider();
    }
}
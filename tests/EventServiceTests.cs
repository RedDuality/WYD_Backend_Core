using Core.Services.Events;
using Core.Services.Profiles;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Components.Database;
using Core.Services.Communities;
using Core.Components.MessageQueue;
using Xunit;
using Moq;
using FluentAssertions;

namespace Core.Tests;

public class EventServiceTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbService _dbService;
    private readonly EventService _sut;

    public EventServiceTests(MongoDbFixture fixture)
    {
        _dbService = new MongoDbService(fixture.DbContext);

        // Use real services for logic that touches the DB
        var eventDetailsService = new EventDetailsService(_dbService);
        var profileEventService = new ProfileEventService(_dbService, new EventProfileService(_dbService));

        // Mock external services that don't belong in a DB test (like Email or Message Queues)
        var mockGroup = new Mock<GroupService>(_dbService);
        var mockMedia = new Mock<MediaService>();
        var mockQueue = new Mock<MessageQueueService>();

        _sut = new EventService(
            _dbService,
            eventDetailsService,
            profileEventService,
            new EventProfileService(_dbService),
            mockGroup.Object,
            mockMedia.Object,
            mockQueue.Object
        );
    }

    [Fact]
    public async Task CreateEventAsync_ShouldPersistEventAndDetailsInDatabase()
    {
        // ARRANGE
        var creator = new Profile("jdoe", "John Doe");
        var request = new CreateEventRequestDto
        {
            Title = "Release Party",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!",
            ProfileIds = [] // Add other IDs here if testing invites
        };

        // ACT
        var response = await _sut.CreateEventAsync(request, creator);

        // ASSERT
        // 1. Check response
        response.Title.Should().Be("Release Party");

        // 2. Check Database directly (the "Gold Standard" of testing)
        var filter = MongoDB.Driver.Builders<Core.Model.Events.Event>.Filter.Eq(e => e.Title, "Release Party");
        var savedEvent = await _dbService.RetrieveAsync(CollectionName.Events, filter);

        savedEvent.Should().NotBeNull();
        savedEvent.Id.ToString().Should().Be(response.Hash);

        // 3. Verify EventDetails were also created (Transaction check)
        var details = await _dbService.RetrieveAsync<Core.Model.Events.EventDetails>(
            CollectionName.EventDetails,
            MongoDB.Driver.Builders<Core.Model.Events.EventDetails>.Filter.Eq("eventId", savedEvent.Id)
        );
        details.Description.Should().Be("Celebrating the new test suite!");
    }
}
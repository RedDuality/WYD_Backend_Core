using Core.Services.Events;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Components.Database;
using Xunit;
using FluentAssertions;
using Core.Tests.Setup;
using Microsoft.Extensions.DependencyInjection;
using Core.Model.Events;
using Core.Model.Communities;
using Core.DTO.CommunityAPI;
using Core.Model.Masks;
using Core.Services.Profiles;

namespace Core.Tests.Events;

public class EventServiceTests : IClassFixture<MongoDbFixture>
{
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly MongoDbService _dbService;

    public EventServiceTests(MongoDbFixture fixture)
    {
        _dbService = fixture.DbService;

        // Resolve the service under test (SUT) and any mocks needed for verification
        var scope = fixture.ServiceProvider.CreateScope();
        
        _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();

        _eventService = scope.ServiceProvider.GetRequiredService<EventService>();
    }

    [Fact]
    public async Task CreateEventAsync_ShouldPersistEventAndDetailsInDatabase()
    {
        // ARRANGE
        var creator = await _profileService.CreateAsync("jdoe", "John Doe", null);

        var request = new CreateEventRequestDto
        {
            Title = "Release Party",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!",
            ProfileIds = []
        };

        // ACT
        var response = await _eventService.CreateEventAsync(request, creator);

        // ASSERT
        await CheckDirectEventCreation(response, request, creator);
    }

    // create and share
    // 1 mask created
    // eventUpdatedAt equal in all profileEvents


    private async Task CheckDirectEventCreation(RetrieveEventResponseDto response, CreateEventRequestDto request, Profile creator)
    {
        // create Event
        response.Title.Should().Be("Release Party");

        var filter = MongoDB.Driver.Builders<Event>.Filter.Eq(e => e.Title, "Release Party");
        var savedEvent = await _dbService.RetrieveAsync(CollectionName.Events, filter);

        savedEvent.Should().NotBeNull();
        savedEvent.Id.ToString().Should().Be(response.Hash);

        // create Details
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            MongoDB.Driver.Builders<EventDetails>.Filter.Eq("eventId", savedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Celebrating the new test suite!");

        // create ProfileEvent
        var peFilter = MongoDB.Driver.Builders<ProfileEvent>.Filter.And(
            MongoDB.Driver.Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, creator.Id),
            MongoDB.Driver.Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, savedEvent.Id)
        );
        var profileEvent = await _dbService.RetrieveAsync(CollectionName.ProfileEvents, peFilter);

        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().BeTrue();
        profileEvent.EventStartTime.Should().BeCloseTo(request.StartTime.ToUniversalTime(), precision: TimeSpan.FromSeconds(1));

        // create EventProfile
        var epFilter = MongoDB.Driver.Builders<EventProfile>.Filter.And(
            MongoDB.Driver.Builders<EventProfile>.Filter.Eq(ep => ep.EventId, savedEvent.Id),
            MongoDB.Driver.Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, creator.Id)
        );

        var eventProfile = await _dbService.RetrieveAsync(CollectionName.EventProfiles, epFilter);

        eventProfile.Should().NotBeNull();
        eventProfile.EventId.Should().Be(savedEvent.Id);
        eventProfile.ProfileId.Should().Be(creator.Id);
    }


    // todo
    // check groupService.GetProfilesByGroupIds

    [Fact]
    public async Task ShareEventAsync_ShouldCreateProfileEventsAndNotCreateMasks()
    {
        // 1. ARRANGE
        var creator = await _profileService.CreateAsync("jdoe", "John Doe", null);
        var sharedProfile = await _profileService.CreateAsync("asmith", "Alice Smith", null);


        // Seed Community and Group so GroupService can resolve the shared profiles
        var community = new Community("Test Community", creator, CommunityType.Personal);
        await _dbService.CreateOneAsync(CollectionName.Communities, community, null);

        var groupProfiles = new HashSet<GroupProfile>
        {
            new(creator, GroupRole.Owner),
            new(sharedProfile, GroupRole.Viewer)
        };

        var group = new Group(community, "Test Group", groupProfiles);
        await _dbService.CreateOneAsync(CollectionName.Groups, group, null);

        // Create an existing event to share
        var ev = new Event("Shared Celebration", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2));
        await _dbService.CreateOneAsync(CollectionName.Events, ev, null);

        var shareRequest = new List<ShareEventRequestDto>
        {
            new() {
                CommunityId = community.Id.ToString(),
                GroupId = group.Id.ToString()
            }
        };

        // 2. ACT
        var response = await _eventService.ShareEventAsync(creator, ev.Id.ToString(), shareRequest);

        // 3. ASSERT
        var peFilter = MongoDB.Driver.Builders<ProfileEvent>.Filter.And(
            MongoDB.Driver.Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, sharedProfile.Id),
            MongoDB.Driver.Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, ev.Id)
        );

        var profileEvent = await _dbService.RetrieveAsync(CollectionName.ProfileEvents, peFilter);

        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().BeFalse(); // Shared users must confirm themselves

        // Verify EventUpdatedAt (eventUpdateDate)
        // It should match the UpdatedAt timestamp of the event at the time of sharing
        profileEvent.EventUpdatedAt.Should().BeExactly(ev.UpdatedAt);

        // No Masks Created
        var maskFilter = MongoDB.Driver.Builders<Mask>.Filter.And(
            MongoDB.Driver.Builders<Mask>.Filter.Eq("profileId", sharedProfile.Id),
            MongoDB.Driver.Builders<Mask>.Filter.Eq("eventId", ev.Id)
        );

        var mask = await _dbService.RetrieveAsync(CollectionName.Masks, maskFilter);
        mask.Should().BeNull("Masks should not be created during a share operation");
    }

}
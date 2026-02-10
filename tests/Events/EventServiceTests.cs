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
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Tests.Events;

[Collection("DatabaseTests")]
public class EventServiceTests
{
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creator;

    private readonly IClientSessionHandle _session;


    public EventServiceTests(MongoDbFixture fixture)
    {
        Skip.If(fixture.InitializationFailed, fixture.InitializationError);

        _dbService = fixture.DbService!;

        var scope = fixture.ServiceProvider!.CreateScope();

        _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        _eventService = scope.ServiceProvider.GetRequiredService<EventService>();

        _session = fixture.StartSessionAsync().GetAwaiter().GetResult();

        string uniqueTag = $"jdoe_{Guid.NewGuid().ToString()[..8]}";
        _creator = _profileService.CreateAsync(uniqueTag, "John Doe", _session).GetAwaiter().GetResult();
    }


    [SkippableFact]
    public async Task CreateEventAsync_ShouldPersistEventAndDetailsInDatabase()
    {
        var request = new CreateEventRequestDto
        {
            Title = "Release Party",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!",
            ProfileIds = []
        };

        // ACT
        var response = await _eventService.CreateEventAsync(request, _creator);

        // ASSERT
        await AssertEventCreation(response, request, _creator);
    }

    private async Task AssertEventCreation(RetrieveEventResponseDto response, CreateEventRequestDto request, Profile creator)
    {
        // create Event
        ObjectId.TryParse(response.Hash, out _).Should().BeTrue("the returned Hash should be a valid 24-character hex ObjectId");
        response.Title.Should().Be("Release Party");

        var filter = Builders<Event>.Filter.Eq(e => e.Title, "Release Party");
        var savedEvent = await _dbService.RetrieveAsync(CollectionName.Events, filter);

        savedEvent.Should().NotBeNull();
        savedEvent.Id.ToString().Should().Be(response.Hash);

        // create Details
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", savedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Celebrating the new test suite!");

        // create ProfileEvent
        var peFilter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, creator.Id),
            Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, savedEvent.Id)
        );
        var profileEvent = await _dbService.RetrieveAsync(CollectionName.ProfileEvents, peFilter);

        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().BeTrue();
        profileEvent.EventStartTime.Should().BeCloseTo(request.StartTime.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1));
        profileEvent.EventEndTime.Should().BeCloseTo(request.EndTime.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1));

        // create EventProfile
        var epFilter = Builders<EventProfile>.Filter.And(
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, savedEvent.Id),
            Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, creator.Id)
        );

        var eventProfile = await _dbService.RetrieveAsync(CollectionName.EventProfiles, epFilter);

        eventProfile.Should().NotBeNull();
        eventProfile.EventId.Should().Be(savedEvent.Id);
        eventProfile.ProfileId.Should().Be(creator.Id);

        var creatorMask = await _dbService.RetrieveOrNullAsync(
            CollectionName.Masks,
            Builders<Mask>.Filter.And(
                Builders<Mask>.Filter.Eq("profileId", creator.Id),
                Builders<Mask>.Filter.Eq("eventId", savedEvent.Id)
            )
        );

        creatorMask.Should().NotBeNull("Creator should have a mask for their own event");
        creatorMask.Title.Should().Be(request.Title);
    }


    // TODO
    // create tests for groupService.GetProfilesByGroupIds

    [SkippableFact]
    public async Task ShareEventAsync_ShouldCreateProfileEventsAndNotCreateMasks()
    {
        // 1. ARRANGE
        var sharedProfile = await _profileService.CreateAsync("asmith", "Alice Smith", _session);

        // Seed Community and Group so GroupService can resolve the shared profiles
        var community = new Community("Test Community", _creator, CommunityType.Personal);
        await _dbService.CreateOneAsync(CollectionName.Communities, community, null);

        var groupProfiles = new HashSet<GroupProfile>
        {
            new(_creator, GroupRole.Owner),
            new(sharedProfile, GroupRole.Viewer)
        };

        var group = new Group(community, "Test Group", groupProfiles);
        await _dbService.CreateOneAsync(CollectionName.Groups, group, null);

        // Create an existing event to share
        var request = new CreateEventRequestDto
        {
            Title = "Shared Celebration",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!",
            ProfileIds = []
        };
        var createdEventDto = await _eventService.CreateEventAsync(request, _creator);

        // 2. ACT
        var shareRequest = new List<ShareEventRequestDto>
        {
            new() {
                CommunityId = community.Id.ToString(),
                GroupId = group.Id.ToString()
            }
        };
        var response = await _eventService.ShareEventAsync(_creator, createdEventDto.Hash.ToString(), shareRequest);
        
        // wait for propagation
        await Task.Delay(200);

        // 3. ASSERT
        // event should have been updated
        var ev = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, createdEventDto.Hash.ToString());

        var peFilter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, sharedProfile.Id),
            Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, ev.Id)
        );
        var profileEvent = await _dbService.RetrieveAsync(CollectionName.ProfileEvents, peFilter);

        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().BeFalse(); // Shared users must not be confirmed

        // Verify EventUpdatedAt (eventUpdateAt)
        // It should match the UpdatedAt timestamp of the event at the time of sharing
        profileEvent.EventUpdatedAt.Should().BeCloseTo(ev.UpdatedAt, precision: TimeSpan.FromMilliseconds(1));

        // verify propagation for the other profiles(creator)
        var creatorPeFilter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creator.Id),
            Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, ev.Id)
        );

        var creatorProfileEvent = await _dbService.RetrieveAsync(CollectionName.ProfileEvents, creatorPeFilter);

        creatorProfileEvent.Should().NotBeNull();
        creatorProfileEvent.Confirmed.Should().BeTrue();

        // Verify propagation (eventUpdateAt)
        // It should match the UpdatedAt timestamp of the event at the time of sharing
        creatorProfileEvent.EventUpdatedAt.Should().BeCloseTo(ev.UpdatedAt, precision: TimeSpan.FromMilliseconds(1));

        // No Masks Created
        var maskFilter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq("profileId", sharedProfile.Id),
            Builders<Mask>.Filter.Eq("eventId", ev.Id)
        );

        var mask = await _dbService.RetrieveOrNullAsync(CollectionName.Masks, maskFilter);
        mask.Should().BeNull("Masks should not be created during a share operation");
    }

    // create and share
    // 1 mask created
    // eventUpdatedAt equal in all profileEvents

    [SkippableFact]
    public async Task CreateEventAsync_WithSharedProfiles_ShouldPersistAllEntitiesAndCreatorMask()
    {
        // 1. ARRANGE
        var invitedProfile = await _profileService.CreateAsync("jsmith", "Jane Smith", _session);

        var request = new CreateEventRequestDto
        {
            Title = "Collaborative Workshop",
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            EndTime = DateTimeOffset.UtcNow.AddDays(2).AddHours(3),
            Description = "A shared event creation test",
            ProfileIds = [invitedProfile.Id.ToString()]
        };

        // 2. ACT
        var response = await _eventService.CreateEventAsync(request, _creator);
        var eventId = new ObjectId(response.Hash);

        // 3. ASSERT - Event & Details
        var savedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, response.Hash);
        savedEvent.Should().NotBeNull();
        savedEvent.TotalProfilesMinusOne.Should().Be(1);

        var details = await _dbService.RetrieveAsync<EventDetails>(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", eventId)
        );
        details.Description.Should().Be(request.Description);

        // 4. ASSERT - ProfileEvents (Creator vs Invited)
        var allProfileEvents = await _dbService.GetCollection<ProfileEvent>(CollectionName.ProfileEvents)
            .Find(Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, eventId))
            .ToListAsync();

        allProfileEvents.Should().HaveCount(2);

        var creatorPE = allProfileEvents.First(pe => pe.ProfileId == _creator.Id);
        var invitedPE = allProfileEvents.First(pe => pe.ProfileId == invitedProfile.Id);

        creatorPE.Confirmed.Should().BeTrue("Creator should be auto-confirmed");
        invitedPE.Confirmed.Should().BeFalse("Invited user should not be confirmed");

        // 5. ASSERT - Timestamp Consistency
        // Both should match the event's UpdatedAt timestamp
        invitedPE.EventUpdatedAt.Should().BeCloseTo(creatorPE.EventUpdatedAt, precision: TimeSpan.FromMilliseconds(1));
        invitedPE.EventUpdatedAt.Should().BeCloseTo(savedEvent.UpdatedAt, precision: TimeSpan.FromMilliseconds(1));

        // 6. ASSERT - Mask logic (Only for creator at creation)
        var creatorMask = await _dbService.RetrieveOrNullAsync<Mask>(
            CollectionName.Masks,
            Builders<Mask>.Filter.And(
                Builders<Mask>.Filter.Eq("profileId", _creator.Id),
                Builders<Mask>.Filter.Eq("eventId", eventId)
            )
        );

        creatorMask.Should().NotBeNull("Creator should have a mask for their own event");
        creatorMask!.Title.Should().Be(request.Title);

        var invitedMask = await _dbService.RetrieveOrNullAsync(
            CollectionName.Masks,
            Builders<Mask>.Filter.And(
                Builders<Mask>.Filter.Eq("profileId", invitedProfile.Id),
                Builders<Mask>.Filter.Eq("eventId", eventId)
            )
        );
        invitedMask.Should().BeNull("Invited users should not get a mask until they confirm");

        // 7. ASSERT - EventProfile links
        var eventProfiles = await _dbService.GetCollection<EventProfile>(CollectionName.EventProfiles)
            .Find(Builders<EventProfile>.Filter.Eq(ep => ep.EventId, eventId))
            .ToListAsync();

        eventProfiles.Should().HaveCount(2, "Both profiles should have an entry in EventProfiles for indexing");
    }
}
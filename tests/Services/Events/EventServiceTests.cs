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
using Core.Services.Events.Instances;

namespace Core.Tests.Services.Events;

[Collection("DatabaseTests")]
public class EventServiceTests {
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creatorProfile;

    private readonly IClientSessionHandle _session;


    public EventServiceTests(MongoDbFixture fixture) {
        Skip.If(fixture.InitializationFailed, fixture.InitializationError);

        _dbService = fixture.DbService!;

        var scope = fixture.ServiceProvider!.CreateScope();

        _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        _eventService = scope.ServiceProvider.GetRequiredService<EventService>();

        _session = fixture.StartSessionAsync().GetAwaiter().GetResult();

        string uniqueTag = $"jdoe_{Guid.NewGuid().ToString()[..8]}";
        _creatorProfile = _profileService.CreateAsync(uniqueTag, "John Doe", _session).GetAwaiter().GetResult();
    }

    #region create & share

    [SkippableFact]
    public async Task CreateEventAsync_ShouldPersistEventAndDetailsInDatabase() {
        var request = new CreateEventRequestDto {
            Title = "Release Party",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!"
        };

        // ACT
        var response = await _eventService.CreateEventAsync(request, _creatorProfile);

        // ASSERT
        await AssertEventCreation(response, request, _creatorProfile);
    }

    private async Task AssertEventCreation(RetrieveEventResponseDto response, CreateEventRequestDto request, Profile creator) {
        // create Event
        ObjectId.TryParse(response.Id, out _).Should().BeTrue("the returned Hash should be a valid 24-character hex ObjectId");
        response.Title.Should().Be("Release Party");

        var filter = Builders<Event>.Filter.Eq(e => e.Title, "Release Party");
        var savedEvent = await _dbService.RetrieveAsync(CollectionName.Events, filter);

        savedEvent.Should().NotBeNull();
        savedEvent.Id.ToString().Should().Be(response.Id);

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
    public async Task ShareEventAsync_ShouldCreateProfileEventsAndNotCreateMasks() {
        // 1. ARRANGE
        var sharedProfile = await _profileService.CreateAsync("asmith", "Alice Smith", _session);

        // Seed Community and Group so GroupService can resolve the shared profiles
        var community = new Community("Test Community", _creatorProfile, CommunityType.Personal);
        await _dbService.CreateOneAsync(CollectionName.Communities, community, null);

        var groupProfiles = new HashSet<GroupProfile>
        {
            new(_creatorProfile, GroupRole.Owner),
            new(sharedProfile, GroupRole.Viewer)
        };

        var group = new Group(community, "Test Group", groupProfiles);
        await _dbService.CreateOneAsync(CollectionName.Groups, group, null);

        // Create an existing event to share
        var request = new CreateEventRequestDto {
            Title = "Shared Celebration",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Celebrating the new test suite!"
        };
        var createdEventDto = await _eventService.CreateEventAsync(request, _creatorProfile);

        // 2. ACT
        var shareRequest = new ShareEventRequestDto {
            SharedGroups = [
                new ShareGroupIdentifierDto
                {
                    CommunityId = community.Id.ToString(),
                    GroupId = group.Id.ToString()
                }
            ]
        };

        var response = await _eventService.ShareEventAsync(_creatorProfile, createdEventDto.Id.ToString(), shareRequest);

        // wait for propagation
        await Task.Delay(200);

        // 3. ASSERT
        // event should have been updated
        var ev = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, createdEventDto.Id.ToString());

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
            Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id),
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
    public async Task CreateEventAsync_WithSharedProfiles_ShouldPersistAllEntitiesAndCreatorMask() {
        // 1. ARRANGE
        var invitedProfile = await _profileService.CreateAsync("jsmith", "Jane Smith", _session);

        var community = new Community("Test Community Two", _creatorProfile, CommunityType.Personal);
        await _dbService.CreateOneAsync(CollectionName.Communities, community, null);

        var groupProfiles = new HashSet<GroupProfile>
        {
            new(_creatorProfile, GroupRole.Owner),
            new(invitedProfile, GroupRole.Viewer)
        };

        var group = new Group(community, "Test Group Two", groupProfiles);
        await _dbService.CreateOneAsync(CollectionName.Groups, group, null);


        var shareRequest = new ShareEventRequestDto {
            SharedGroups = [
                new ShareGroupIdentifierDto
                {
                    CommunityId = community.Id.ToString(),
                    GroupId = group.Id.ToString()
                }
            ]
        };

        var request = new CreateEventRequestDto {
            Title = "Collaborative Workshop",
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            EndTime = DateTimeOffset.UtcNow.AddDays(2).AddHours(3),
            Description = "A shared event creation test",
            ShareDto = shareRequest
        };

        // 2. ACT
        var response = await _eventService.CreateEventAsync(request, _creatorProfile);
        var eventId = new ObjectId(response.Id);

        // 3. ASSERT - Event & Details
        var savedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, response.Id);
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

        var creatorPE = allProfileEvents.First(pe => pe.ProfileId == _creatorProfile.Id);
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
                Builders<Mask>.Filter.Eq("profileId", _creatorProfile.Id),
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
    #endregion

    #region modify

    [SkippableFact]
    public async Task UpdateEvent_ShouldUpdateEventAndDetails() {
        // 1. ARRANGE
        var createRequest = new CreateEventRequestDto {
            Title = "Original Title",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Original description"
        };
        var created = await _eventService.CreateEventAsync(createRequest, _creatorProfile);

        var updateDto = new UpdateEventRequestDto {
            EventId = created.Id,
            Title = "Updated Title",
            Description = "Updated description",
            StartTime = DateTimeOffset.UtcNow.AddDays(3),
            EndTime = DateTimeOffset.UtcNow.AddDays(3).AddHours(4)
        };

        // 2. ACT
        await _eventService.UpdateEvent(updateDto, _session);

        // 3. ASSERT - Event document
        var updatedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, created.Id);
        updatedEvent.Title.Should().Be("Updated Title");
        updatedEvent.StartTime.Should().BeCloseTo(updateDto.StartTime.Value.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1));
        updatedEvent.EndTime.Should().BeCloseTo(updateDto.EndTime.Value.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1));

        // 4. ASSERT - EventDetails document
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", updatedEvent.Id)
        );
        details.Description.Should().Be("Updated description");
    }

    [SkippableFact]
    public async Task UpdateEvent_WithNullTitle_ShouldNotChangeTitle() {
        // 1. ARRANGE
        var createRequest = new CreateEventRequestDto {
            Title = "Original Title",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Original description"
        };
        var created = await _eventService.CreateEventAsync(createRequest, _creatorProfile);

        var updateRequest = new UpdateEventRequestDto {
            EventId = created.Id,
            Title = null,
            Description = "Updated description",
            StartTime = DateTimeOffset.UtcNow.AddDays(3),
            EndTime = DateTimeOffset.UtcNow.AddDays(3).AddHours(4)
        };

        // 2. ACT
        await _eventService.UpdateEvent(updateRequest, _session);

        // 3. ASSERT
        var updatedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, created.Id);
        updatedEvent.Title.Should().Be("Original Title", "a null Title in the request should leave the field unchanged");
    }

    [SkippableFact]
    public async Task UpdateEvent_WithNullDescription_ShouldNotChangeDescription() {
        // 1. ARRANGE
        var createRequest = new CreateEventRequestDto {
            Title = "Original Title",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Description = "Original description"
        };
        var created = await _eventService.CreateEventAsync(createRequest, _creatorProfile);

        var updateRequest = new UpdateEventRequestDto {
            EventId = created.Id,
            Title = "Updated Title",
            Description = null,
            StartTime = DateTimeOffset.UtcNow.AddDays(3),
            EndTime = DateTimeOffset.UtcNow.AddDays(3).AddHours(4)
        };

        // 2. ACT
        await _eventService.UpdateEvent(updateRequest, _session);

        // 3. ASSERT
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", new ObjectId(created.Id))
        );
        details.Description.Should().Be("Original description", "a null Description in the request should leave the field unchanged");
    }

    [SkippableFact]
    public async Task UpdateEvent_WithNullTimes_ShouldNotChangeTimes() {
        // 1. ARRANGE
        var originalStart = DateTimeOffset.UtcNow.AddDays(1);
        var originalEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2);

        var createRequest = new CreateEventRequestDto {
            Title = "Original Title",
            StartTime = originalStart,
            EndTime = originalEnd,
            Description = "Original description"
        };
        var created = await _eventService.CreateEventAsync(createRequest, _creatorProfile);

        var updateRequest = new UpdateEventRequestDto {
            EventId = created.Id,
            Title = "Updated Title",
            Description = "Updated description",
            StartTime = null,
            EndTime = null
        };

        // 2. ACT
        await _eventService.UpdateEvent(updateRequest, _session);

        // 3. ASSERT
        var updatedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, created.Id);
        updatedEvent.StartTime.Should().BeCloseTo(originalStart.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1),
            because: "a null StartTime in the request should leave the field unchanged");
        updatedEvent.EndTime.Should().BeCloseTo(originalEnd.ToUniversalTime(), precision: TimeSpan.FromMilliseconds(1),
            because: "a null EndTime in the request should leave the field unchanged");
    }


    #endregion

}
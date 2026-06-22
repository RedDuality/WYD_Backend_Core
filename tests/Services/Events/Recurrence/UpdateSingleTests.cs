using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Components.Database;
using Xunit;
using FluentAssertions;
using Core.Tests.Setup;
using Microsoft.Extensions.DependencyInjection;
using Core.Model.Events;
using Core.Services.Profiles;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Services.Events.Recurrence;
using Core.Model.Events.Recurrence;
using Core.Services.Events.Instances;

namespace Core.Tests.Services.Events.Recurrence;

[Collection("DatabaseTests")]
public class UpdateSingleTests {
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly RecurrentEventService _recurrentEventService;
    private readonly RecurrentEventUpdateService _recurrentUpdateService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creatorProfile;

    private readonly IClientSessionHandle _session;

    public UpdateSingleTests(MongoDbFixture fixture) {
        Skip.If(fixture.InitializationFailed, fixture.InitializationError);

        _dbService = fixture.DbService!;

        var scope = fixture.ServiceProvider!.CreateScope();

        _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        _eventService = scope.ServiceProvider.GetRequiredService<EventService>();
        _recurrentEventService = scope.ServiceProvider.GetRequiredService<RecurrentEventService>();
        _recurrentUpdateService = scope.ServiceProvider.GetRequiredService<RecurrentEventUpdateService>();

        _session = fixture.StartSessionAsync().GetAwaiter().GetResult();

        string uniqueTag = $"jdoe_{Guid.NewGuid().ToString()[..8]}";
        _creatorProfile = _profileService.CreateAsync(uniqueTag, "John Doe", _session).GetAwaiter().GetResult();
    }

    #region util
    private async Task<RetrieveRecurrentEventResponseDto> BuildMasterAsync(
        string title = "Team Standup",
        string rrule = "FREQ=DAILY;COUNT=5",
        string timeZone = "UTC",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? description = null) {

        var s = start ?? new DateTimeOffset(2025, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var e = end ?? s.AddHours(1);

        var requestDto = new CreateRecurrentEventRequestDto {
            Title = title,
            RecurrenceRule = rrule,
            TimeZone = timeZone,
            StartTime = s,
            EndTime = e,
            CacheIntervalStart = s,
            CacheIntervalEnd = s.AddMonths(1),
            Description = description
        };

        var responseDto = await _recurrentEventService.CreateRecurrentEventAsync(requestDto, _creatorProfile);
        return responseDto;
    }

    #endregion

    #region exceptions

    [SkippableFact]
    public async Task UpdateSingleInstance_ShouldThrow_WhenUpdateTypeIsInvalid() {
        // ARRANGE
        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence, // Wrong type for this method
            InstanceId = "any_id",
            MasterEventId = ObjectId.GenerateNewId().ToString()
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(request, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateSingleInstance_ShouldThrow_WhenRecurrenceRuleIsProvided() {
        // ARRANGE
        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            InstanceId = "any_id",
            MasterEventId = ObjectId.GenerateNewId().ToString(),
            RecurrenceRule = "FREQ=DAILY" // Should not be here
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(request, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateSingleInstance_ShouldThrow_NoUpdatesWereMade() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));

        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = ""
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile));

        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime,
            EndTime = startTime.AddHours(1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile));

        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = null,
            EndTime = startTime.AddHours(1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto4, _creatorProfile));

        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime,
            EndTime = null
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto5, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateSingleInstance_ShouldThrow_EmptyTitle() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = ""
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));
    }

    #endregion

    #region create
    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithNewTitle() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");
        detachedEvent.StartTime.Should().Equals(master.StartTime);
        detachedEvent.EndTime.Should().Equals(master.EndTime);
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be(null);

        // ASSERT: EventProfiles
        var eventProfile = await _dbService.RetrieveAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.And(
                Builders<EventProfile>.Filter.Eq(ep => ep.EventId, detachedEvent.Id),
                Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
            )
        );
        eventProfile.Should().NotBeNull();


        // ASSERT: EventProfiles
        var profileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, detachedEvent.Id),
                Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );

        var masterProfileEvent= await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().Equals(masterProfileEvent.Confirmed);
        profileEvent.EventStartTime.Should().Equals(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Equals(detachedEvent.EndTime);
        profileEvent.Role.Should().Equals(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Equals(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Equals(detachedEvent.Id);
        detachedListInstance.StartTime.Should().Equals(detachedEvent.StartTime);
    }

    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithNewTitle() {
        //TODO
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // 3. ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");
        detachedEvent.StartTime.Should().Equals(master.StartTime);
        detachedEvent.EndTime.Should().Equals(master.EndTime);
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();


        // 4. ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.Instances.Should().ContainSingle(i =>
            i.EventId == detachedEvent.Id && i.RecurrencyId == instanceId);

        // 5. ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Description.Should().Be(null);
    }

    [SkippableFact]
    public async Task CreateDetachedInstance_ShouldSucceed_AndPopulateCollections() {
        // 1. ARRANGE: Create a Master Recurrent Event
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session",
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // 3. ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // 4. ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.Instances.Should().ContainSingle(i =>
            i.EventId == detachedEvent.Id && i.RecurrencyId == instanceId);

        // 5. ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Description.Should().Be("Bring your own mat today!");
    }

    #endregion

    #region already existing

    [SkippableFact]
    public async Task UpdateDetachedInstance_ShouldUpdateExistingEventAndDetachedInstancesCollection() {
        // 1. ARRANGE: Create a Master Recurrent Event
        var masterStartTime = DateTimeOffset.UtcNow.AddDays(5);

        var master = await BuildMasterAsync(
            "Original Master Title",
            "FREQ=DAILY",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1)
        );

        // Create a Detached Event (already persisted in 'events' collection)
        var instanceId = $"{master.Id}_{masterStartTime:yyyyMMddTHHmmssZ}";
        var existingDetachedEvent = new Event("Initial Detached Title", masterStartTime, masterStartTime.AddHours(1)) {
            MasterEventId = new ObjectId(master.Id),
            RecurrencyInstanceId = instanceId,
            DetachedInstance = true
        };
        await _eventService.CreateEvent(existingDetachedEvent, _creatorProfile, [], "", _session);


        // Register it in the DetachedInstances tracker
        var tracker = new DetachedInstances(new ObjectId(master.Id), [
            new DetachedInstance(existingDetachedEvent.Id, instanceId, existingDetachedEvent.StartTime)
        ]);
        await _dbService.CreateOneAsync(CollectionName.DetachedInstances, tracker, _session);

        // Prepare Update Request (using the Event's ObjectId, not the compound string)
        var newStartTime = masterStartTime.AddHours(2);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = existingDetachedEvent.Id.ToString(), // Existing EventId
            Title = "Final Updated Title",
            Description = "New Description",
            StartTime = newStartTime,
            EndTime = newStartTime.AddHours(1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // 3. ASSERT: Event document updates
        var updatedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, existingDetachedEvent.Id.ToString());
        updatedEvent.Title.Should().Be("Final Updated Title");
        updatedEvent.StartTime.Should().BeCloseTo(newStartTime, TimeSpan.FromMilliseconds(1));

        // 4. ASSERT: Tracker update (startTime in DetachedInstances collection)
        var updatedTracker = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        var instanceInTracker = updatedTracker.Instances.First(i => i.EventId == existingDetachedEvent.Id);
        instanceInTracker.StartTime.Should().BeCloseTo(newStartTime, TimeSpan.FromMilliseconds(1));

        // 5. ASSERT: Details update
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", existingDetachedEvent.Id)
        );
        details.Description.Should().Be("New Description");
    }

    [SkippableFact]
    public async Task UpdateDetachedInstance_ShouldThrow_WhenNoDetachedInstancesExists() {
        // ARRANGE: Event exists but isn't registered in DetachedInstances collection
        var masterId = ObjectId.GenerateNewId();
        var detachedEventId = ObjectId.GenerateNewId();

        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = masterId.ToString(),
            InstanceId = detachedEventId.ToString()
        };

        // ACT & ASSERT: Should throw because the detachedInstances does not include the event
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(request, _creatorProfile));
    }

    #endregion
}
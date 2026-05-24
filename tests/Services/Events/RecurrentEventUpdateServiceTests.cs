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

namespace Core.Tests.Services.Events;

[Collection("DatabaseTests")]
public class RecurrentEventUpdateServiceTests {
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly RecurrentEventService _recurrentEventService;
    private readonly RecurrentEventUpdateService _recurrentUpdateService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creatorProfile;

    private readonly IClientSessionHandle _session;

    public RecurrentEventUpdateServiceTests(MongoDbFixture fixture) {
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

    #region single

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
        await _eventService.CreateEvent(existingDetachedEvent, _creatorProfile, [], "",_session);


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

    #region sequence 

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenUpdateTypeIsInvalid() {
        // ARRANGE
        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance, // Invalid for this entry point switch
            MasterEventId = ObjectId.GenerateNewId().ToString(),
            InstanceId = "some_id"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(request, _creatorProfile));
    }

    #region allsequence

    [SkippableFact]
    public async Task UpdateAllTheSequence_ShouldUpdateMasterAndDetails() {
        // 1. ARRANGE: Create a Master Recurrent Event with Details
        var master = await BuildMasterAsync(
            "Old Title",
            "FREQ=DAILY",
            "UTC",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddHours(2),
            "Old Desc"
        );

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = $"{master.Id}_20260524T120000Z", // A generated ID (contains MasterId)
            Title = "New Global Title",
            Description = "New Global Description",
            RecurrenceRule = "FREQ=WEEKLY"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // 3. ASSERT: Master RecurrentEvent document
        var updatedMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id.ToString());
        updatedMaster.Title.Should().Be("New Global Title");
        updatedMaster.RecurrenceRule.Should().Be("FREQ=WEEKLY");

        // 4. ASSERT: EventDetails document
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq(ed => ed.EventId, new ObjectId(master.Id))
        );
        details.Description.Should().Be("New Global Description");
    }

    [SkippableFact]
    public async Task UpdateAllTheSequence_ShouldAlsoUpdateDetachedInstance_WhenIdIsDetached() {
        // 1. ARRANGE: Create Master and a Detached Instance
        var master = await BuildMasterAsync(
            "Master",
            "FREQ=DAILY",
            "UTC",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1)
        );

        // Create a Detached Event (already persisted in 'events' collection)
        var instanceId = $"{master.Id}_{master.StartTime:yyyyMMddTHHmmssZ}";
        var existingDetachedEvent = new Event("Initial Detached Title", master.StartTime, master.EndTime) {
            MasterEventId = new ObjectId(master.Id),
            RecurrencyInstanceId = instanceId,
            DetachedInstance = true
        };
        await _eventService.CreateEvent(existingDetachedEvent, _creatorProfile, [], "",_session);

        // Register it in the DetachedInstances tracker
        var tracker = new DetachedInstances(new ObjectId(master.Id), [
            new DetachedInstance(existingDetachedEvent.Id, instanceId, existingDetachedEvent.StartTime)
        ]);
        await _dbService.CreateOneAsync(CollectionName.DetachedInstances, tracker, _session);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = existingDetachedEvent.Id.ToString(),
            Title = "Common Title",
            Description = "Common Description"
        };

        // 2. ACT
        await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // 3. ASSERT: Master was updated
        var updatedMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id.ToString());
        updatedMaster.Title.Should().Be("Common Title");

        // 4. ASSERT: Detached Instance was ALSO updated (logic: if !InstanceEventId.Contains(MasterEventId))
        var updatedDetached = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, existingDetachedEvent.Id.ToString());
        updatedDetached.Title.Should().Be("Common Title");

        //master details?
        //detached instance details?
    }

    #endregion

    #region thisandfollowing

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_ShouldSplitSeries_FromGeneratedInstance() {
        // 1. ARRANGE: Create an Old Master Event starting tomorrow, repeating for 10 days
        var masterStart = DateTimeOffset.UtcNow.AddDays(1);
        var oldMaster = await BuildMasterAsync(
            "Morning Standup",
            "FREQ=DAILY;COUNT=10",
            "UTC",
            masterStart,
            masterStart.AddHours(1)
        );

        // Prepare to split at Day 5
        var splitTime = masterStart.AddDays(4);
        var instanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = instanceId,
            Title = "Updated Standup (Going Forward)",
            RecurrenceRule = "FREQ=DAILY;COUNT=5" // New rule going forward
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // 3. ASSERT: Old Master should be truncated
        var truncatedOldMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(
            CollectionName.RecurrentEvents,
            oldMaster.Id.ToString()
        );
        // The stopTime calculation subtracts 2 seconds from the target start time
        var expectedStopTime = splitTime.AddSeconds(-2);
        truncatedOldMaster.RecurrenceEnd.Should().BeCloseTo(expectedStopTime, TimeSpan.FromMilliseconds(999)); //adjusts for utcnow milliseconds

        // 4. ASSERT: New Master should be created with updated properties
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.Should().NotBeNull();
        newMaster.Title.Should().Be("Updated Standup (Going Forward)");
        newMaster.RecurrenceRule.Should().Be("FREQ=DAILY;COUNT=5");
        // Time should match the split time, since updateDto.StartTime wasn't explicitly provided
        newMaster.StartTime.Should().BeCloseTo(splitTime, TimeSpan.FromMilliseconds(999));
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_ShouldMigrateDetachedInstances_ToNewMaster() {
        // 1. ARRANGE: Create Old Master
        var masterStart = DateTimeOffset.UtcNow.AddDays(1);
        var oldMaster = new RecurrentEvent("Master", masterStart, masterStart.AddHours(1), TimeZoneInfo.Utc, "FREQ=DAILY;COUNT=10");
        await _dbService.CreateOneAsync(CollectionName.RecurrentEvents, oldMaster, _session);

        // Detached 1 (Before Split) - Day 2
        var day2Start = masterStart.AddDays(1);
        var detachedBefore = new Event("Detached Before", day2Start, day2Start.AddHours(1)) {
            MasterEventId = oldMaster.Id,
            RecurrencyInstanceId = $"{oldMaster.Id}_{day2Start:yyyyMMddTHHmmssZ}",
            DetachedInstance = true
        };
        await _dbService.CreateOneAsync(CollectionName.Events, detachedBefore, _session);

        // Detached 2 (After Split) - Day 7
        var day7Start = masterStart.AddDays(6);
        var detachedAfter = new Event("Detached After", day7Start, day7Start.AddHours(1)) {
            MasterEventId = oldMaster.Id,
            RecurrencyInstanceId = $"{oldMaster.Id}_{day7Start:yyyyMMddTHHmmssZ}",
            DetachedInstance = true
        };
        await _dbService.CreateOneAsync(CollectionName.Events, detachedAfter, _session);

        // Track both instances on the old master
        var tracker = new DetachedInstances(oldMaster.Id, [
            new DetachedInstance(detachedBefore.Id, detachedBefore.RecurrencyInstanceId, detachedBefore.StartTime),
        new DetachedInstance(detachedAfter.Id, detachedAfter.RecurrencyInstanceId, detachedAfter.StartTime)
        ]);
        await _dbService.CreateOneAsync(CollectionName.DetachedInstances, tracker, _session);

        // Split at Day 5
        var splitTime = masterStart.AddDays(4);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);
        var newMasterId = new ObjectId(result.Id);

        // 3. ASSERT: Old Master Tracker
        // Instances <= stopTime should stay with old master
        var updatedOldTracker = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMaster.Id)
        );
        updatedOldTracker.Instances.Should().ContainSingle(i => i.EventId == detachedBefore.Id);

        // 4. ASSERT: New Master Tracker
        // Instances > stopTime should migrate to a new tracker for the new master
        var newTracker = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, newMasterId)
        );
        newTracker.Should().NotBeNull("A new tracker should be created for the migrated instances mapped to newMaster.Id");
        newTracker.Instances.Should().ContainSingle(i => i.EventId == detachedAfter.Id);

        // 5. ASSERT: Event document migration
        // The Event document for 'detachedAfter' should point to the new Master ID
        var migratedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, detachedAfter.Id.ToString());
        migratedEvent.MasterEventId.Should().Be(newMasterId);
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_ShouldUpdateDetachedInstanceProperties_IfTriggeredOnIt() {
        // 1. ARRANGE
        var masterStart = DateTimeOffset.UtcNow.AddDays(1);
        var oldMaster = new RecurrentEvent("Master", masterStart, masterStart.AddHours(1), TimeZoneInfo.Utc, "FREQ=DAILY;COUNT=10");
        await _dbService.CreateOneAsync(CollectionName.RecurrentEvents, oldMaster, _session);

        // The trigger instance is ALREADY detached
        var targetStart = masterStart.AddDays(4);
        var detachedTarget = new Event("Target Event", targetStart, targetStart.AddHours(1)) {
            MasterEventId = oldMaster.Id,
            RecurrencyInstanceId = $"{oldMaster.Id}_{targetStart:yyyyMMddTHHmmssZ}",
            DetachedInstance = true
        };
        await _dbService.CreateOneAsync(CollectionName.Events, detachedTarget, _session);
        await _dbService.CreateOneAsync(CollectionName.EventDetails, new EventDetails(detachedTarget) { Description = "Old Detached Desc" }, _session);

        var tracker = new DetachedInstances(oldMaster.Id, [
            new DetachedInstance(detachedTarget.Id, detachedTarget.RecurrencyInstanceId, detachedTarget.StartTime)
        ]);
        await _dbService.CreateOneAsync(CollectionName.DetachedInstances, tracker, _session);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = detachedTarget.Id.ToString(), // Target is the detached event itself
            Title = "Brand New Era",
            Description = "Description for the split point"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // 3. ASSERT: The specific detached event properties were overridden during migration
        var updatedTargetEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, detachedTarget.Id.ToString());

        // Properties should be updated via GetInstanceUpdates() since ID matched
        updatedTargetEvent.Title.Should().Be("Brand New Era");
        updatedTargetEvent.MasterEventId.Should().Be(new ObjectId(result.Id));

        // Ensure details were updated
        var details = await _dbService.RetrieveAsync<EventDetails>(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedTarget.Id)
        );
        details.Description.Should().Be("Description for the split point");
    }

    #endregion

    #endregion
}
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
        await _eventService.CreateEvent(existingDetachedEvent, _creatorProfile, [], "", _session);

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

    // -------------------------------------------------------------------------
    // Edge-case tests added to cover COUNT reduction, StartTime/EndTime
    // correctness on the new master, old-rule truncation, and boundary splits.
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_NewMasterStartTime_ShouldBeSplitOccurrence_WhenNotExplicitlyProvided() {
        // ARRANGE: daily series; split at day 5 without providing an explicit StartTime
        var masterStart = new DateTimeOffset(2025, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Daily", "FREQ=DAILY;COUNT=10", "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(4); // day-5 occurrence
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}",
            Title = "New Title"
            // No StartTime provided — new master must derive it from the split occurrence
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: new master starts exactly at the split occurrence, not at the old series origin
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.StartTime.Should().BeCloseTo(splitTime, TimeSpan.FromSeconds(1),
            "the new master's first occurrence must be the split point, not the old series origin");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_NewMasterEndTime_ShouldPreserveDuration_WhenNotExplicitlyProvided() {
        // ARRANGE: 1-hour daily event; split at day 3 — no EndTime in the DTO
        var masterStart = new DateTimeOffset(2025, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var oldMaster = await BuildMasterAsync("Hourly", "FREQ=DAILY;COUNT=7", "UTC", masterStart, masterEnd);

        var splitTime = masterStart.AddDays(2);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: new master preserves the original 1-hour duration
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        var newDuration = newMaster.EndTime - newMaster.StartTime;
        newDuration.Should().Be(TimeSpan.FromHours(1),
            "duration should be inherited from the old master when EndTime is not overridden");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_CountRule_ShouldReduceCountOnNewMaster() {
        // ARRANGE: COUNT=10, split at the 5th occurrence (day index 4) — new master should get COUNT=6
        var masterStart = new DateTimeOffset(2025, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Count-10", "FREQ=DAILY;COUNT=10", "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(4); // 5th occurrence (0-indexed day 4)
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
            // No overriding RecurrenceRule — the COUNT must be recalculated
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: new master carries the remaining 6 occurrences
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.RecurrenceRule.Should().Contain("COUNT=6",
            "occurrences 5-10 (6 total) belong to the new master");

        // ASSERT: old master has been capped and no longer carries the full count
        var truncatedOld = await _dbService.RetrieveByIdAsync<RecurrentEvent>(
            CollectionName.RecurrentEvents, oldMaster.Id.ToString());
        truncatedOld.RecurrenceRule.Should().NotContain("COUNT=",
            "TruncateRuleUntil replaces COUNT with UNTIL on the old master");
        truncatedOld.RecurrenceRule.Should().Contain("UNTIL=",
            "old master must have an UNTIL clause after truncation");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_CountRule_ShouldGiveCount1_WhenSplitAtLastOccurrence() {
        // ARRANGE: COUNT=5, split at the very last occurrence (day 4)
        var masterStart = new DateTimeOffset(2025, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Count-5", "FREQ=DAILY;COUNT=5", "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(4); // last (5th) occurrence
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: new master gets exactly COUNT=1 (clamped to minimum of 1)
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.RecurrenceRule.Should().Contain("COUNT=1");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_CountRule_ShouldGiveFullCount_WhenSplitAtFirstOccurrence() {
        // ARRANGE: COUNT=5, split at the first occurrence (day 0) — new master keeps all 5
        var masterStart = new DateTimeOffset(2025, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Count-5-First", "FREQ=DAILY;COUNT=5", "UTC", masterStart, masterStart.AddHours(1));

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{masterStart:yyyyMMddTHHmmssZ}" // first occurrence
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: no occurrences before the split → new master keeps all 5
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.RecurrenceRule.Should().Contain("COUNT=5");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_UntilRule_ShouldCarryUntilToNewMaster_Unchanged() {
        // ARRANGE: rule with UNTIL — the new master should inherit the same UNTIL
        var masterStart = new DateTimeOffset(2025, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2025, 11, 30, 23, 59, 59, TimeSpan.Zero);
        var rrule = $"FREQ=DAILY;UNTIL={until:yyyyMMddTHHmmssZ}";
        var oldMaster = await BuildMasterAsync("Until-Rule", rrule, "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(14);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: new master carries the original UNTIL date
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.RecurrenceRule.Should().Contain($"UNTIL={until:yyyyMMddTHHmmssZ}",
            "when no explicit rule is provided, the UNTIL clause is inherited from the old master");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_ExplicitRecurrenceRule_ShouldOverrideCountReduction() {
        // ARRANGE: old rule has COUNT=10 but caller supplies a brand-new rule — no reduction should happen
        var masterStart = new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Count-10", "FREQ=DAILY;COUNT=10", "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(4);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}",
            RecurrenceRule = "FREQ=WEEKLY;COUNT=3" // explicit override
        };

        // ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: exact rule as supplied, no automatic COUNT adjustment
        var newMaster = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);
        newMaster.RecurrenceRule.Should().Be("FREQ=WEEKLY;COUNT=3");
    }

    [SkippableFact]
    public async Task UpdateThisAndAllFollowing_OldMasterRule_ShouldContainUntilNotCount_AfterSplit() {
        // ARRANGE: old rule is COUNT-based
        var masterStart = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
        var oldMaster = await BuildMasterAsync("Old Count", "FREQ=DAILY;COUNT=8", "UTC", masterStart, masterStart.AddHours(1));

        var splitTime = masterStart.AddDays(3);
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
            MasterEventId = oldMaster.Id.ToString(),
            InstanceId = $"{oldMaster.Id}_{splitTime:yyyyMMddTHHmmssZ}"
        };

        // ACT
        await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: TruncateRuleUntil must convert the old master's COUNT to an UNTIL
        var truncatedOld = await _dbService.RetrieveByIdAsync<RecurrentEvent>(
            CollectionName.RecurrentEvents, oldMaster.Id.ToString());
        truncatedOld.RecurrenceRule.Should().NotContain("COUNT=",
            "COUNT clause must be replaced by UNTIL on the truncated old master");
        truncatedOld.RecurrenceRule.Should().Contain("UNTIL=");

        // And the UNTIL must be before the split occurrence
        var untilValue = truncatedOld.RecurrenceRule
            .Split(';')
            .First(p => p.StartsWith("UNTIL="))["UNTIL=".Length..];
        var parsedUntil = DateTimeOffset.ParseExact(
            untilValue, "yyyyMMddTHHmmssZ",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal);
        parsedUntil.Should().BeBefore(splitTime,
            "the old master's UNTIL must exclude the split occurrence");
    }
    
    #endregion

    #endregion
}
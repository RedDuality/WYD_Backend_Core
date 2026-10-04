using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Components.Database;
using Xunit;
using FluentAssertions;
using Core.Tests.Setup;
using Core.Model.Events;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Model.Events.Recurrence;

namespace Core.Tests.Services.Events.Recurrence;

[Collection("DatabaseTests")]
public class UpdateThisAndAllFollowingTests(MongoDbFixture fixture) : RecurrentEventTestBase(fixture){

  #region exceptions

    [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_NoUpdatesWereMade() {
    // ARRANGE
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1)
    );

    var updateDto = GetUpdateDto(
      master.Id,
      startTime,
      RecurrentUpdateType.ThisAndAllFollowing
    );

    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));

    var updateDto1 = GetUpdateDto(
      master.Id,
      startTime,
      RecurrentUpdateType.ThisAndAllFollowing,
      endTime: startTime.AddHours(1) 
    );

    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto1, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_EmptyTitle() {
    // ARRANGE
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1)
    );

    var updateDto = GetUpdateDto(
      master.Id,
      startTime,
      RecurrentUpdateType.ThisAndAllFollowing,
      title: ""
    );

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_WithWrongTime() {
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

    // only start
    var updateDto = GetUpdateDto(
      master.Id,
      startTime.AddDays(21),
      RecurrentUpdateType.ThisAndAllFollowing,
      updateStartTime: startTime.AddHours(4)
    );
    
    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));

    //only end
    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      EndTime = startTime.AddHours(3)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto1, _creatorProfile));

    //end is before start
    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime,
      EndTime = startTime.AddHours(-2)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto2, _creatorProfile));

    //(only end) end is equal start
    var updateDto3 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime,
      EndTime = startTime
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));

    // start is after end
    var updateDto4 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(6),
      EndTime = startTime.AddHours(5)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto4, _creatorProfile));

    //start is equal end
    var updateDto5 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(5),
      EndTime = startTime.AddHours(5)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto5, _creatorProfile));


    //start is within less than 5 mins to end
    var updateDto6 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(5),
      EndTime = startTime.AddHours(5).AddMinutes(4)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto6, _creatorProfile));

    // StartTime is not UTC (e.g., UTC+2)
    var updateDtoNonUtcStart = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, startTime.Hour - 1, 0, 0, TimeSpan.FromHours(2)),
      EndTime = startTime.AddHours(8)
    };

    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDtoNonUtcStart, _creatorProfile));

    // EndTime is not UTC (e.g., UTC-5)
    var updateDtoNonUtcEnd = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime,
      EndTime = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, startTime.Hour + 2, 0, 0, TimeSpan.FromHours(-5))
    };

    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDtoNonUtcEnd, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateRecurrentEventInstance_ShouldThrow_WhenDatesShiftToDifferentDayInLocalTimeZone() {
    // ARRANGE
    // Using UTC+9 (Tokyo) to create a clear offset from UTC midnight.
    var timeZoneId = "Tokyo Standard Time";

    // Base Start Time: 2026-07-05 12:00:00 UTC
    // Local Tokyo Time: 2026-07-05 21:00:00 (9:00 PM)
    // Local Midnight boundary for July 5th in UTC: 2026-07-04 15:00:00 UTC to 2026-07-05 14:59:59 UTC
    var startTimeUtc = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    var endTimeUtc = startTimeUtc.AddHours(1); // 2026-07-05 13:00:00 UTC (22:00 Local)

    var master = await BuildMasterAsync(
        "Timezone Boundary Check",
        "FREQ=WEEKLY;INTERVAL=1",
        timeZoneId,
        startTimeUtc,
        endTimeUtc
    );

    // Generate instanceId
    var datePart = startTimeUtc.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    // ACT & ASSERT

    // 1. Both shift to the PREVIOUS day in local time.
    // Start: Local July 4th 22:00 | End: Local July 4th 23:00
    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(2026, 7, 4, 13, 0, 0, TimeSpan.Zero),
      EndTime = new DateTimeOffset(2026, 7, 4, 14, 0, 0, TimeSpan.Zero)
    };
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto1, _creatorProfile));

    // 2. Both shift to the NEXT day in local time.
    // Start: Local July 6th 01:00 | End: Local July 6th 02:00
    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(2026, 7, 5, 16, 0, 0, TimeSpan.Zero),
      EndTime = new DateTimeOffset(2026, 7, 5, 17, 0, 0, TimeSpan.Zero)
    };
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto2, _creatorProfile));

    // 3. Start is valid (July 5), but End spills to the NEXT day in local time.
    // Start: Local July 5th 23:00 | End: Local July 6th 00:30
    var updateDto3 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(2026, 7, 5, 14, 0, 0, TimeSpan.Zero),
      EndTime = new DateTimeOffset(2026, 7, 5, 15, 30, 0, TimeSpan.Zero)
    };
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));

    // 4. Start begins PREVIOUS day, but End is valid (July 5) in local time.
    // Start: Local July 4th 23:00 | End: Local July 5th 01:00
    var updateDto4 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(2026, 7, 4, 14, 0, 0, TimeSpan.Zero),
      EndTime = new DateTimeOffset(2026, 7, 4, 16, 0, 0, TimeSpan.Zero)
    };
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto4, _creatorProfile));

    // 5. Spanning completely out of bounds: Starts previous day, ends next day.
    // Start: Local July 4th 23:00 | End: Local July 6th 01:00
    var updateDto5 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = new DateTimeOffset(2026, 7, 4, 14, 0, 0, TimeSpan.Zero),
      EndTime = new DateTimeOffset(2026, 7, 5, 16, 0, 0, TimeSpan.Zero)
    };
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto5, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_WhenTimesUpdatedFromPastEvent() {

    // Generated

    // ARRANGE
    // Start 5 days ago so it is strictly in the past day-wise
    var startTime = DateTimeOffset.UtcNow.AddDays(-5);

    var master = await BuildMasterAsync(
        "Past Standup",
        "FREQ=DAILY;COUNT=10",
        "UTC",
        startTime,
        startTime.AddHours(1)
    );

    // Generate ID for the FIRST instance (from 5 days ago)
    var recurrencyId = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{recurrencyId}";

    // Attempting to update the Time from a past instance
    var updateDtoStartTime = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(2),
      EndTime = startTime.AddHours(3)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDtoStartTime, _creatorProfile));

    // Detached

    // Detach the first instance from 5 days ago (allowed since we aren't shifting AllTheSequence times yet)
    var detachDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Past Standup (Detached)"
    };
    var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

    // Attempt to update Time for AllTheSequence via the past detached instance
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = detachedResult.Id.ToString(),
      StartTime = startTime.AddHours(2),
      EndTime = startTime.AddHours(3)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_WhenTimesUpdatedFromFirstInstance() {

    // Generated

    // ARRANGE
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1)
    );

    // Generate a valid InstanceId for a FIRST occurrence
    var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    // Attempting to update the StartTime from first instance
    var updateDtoStartTime = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(2),
      EndTime = startTime.AddHours(3)
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDtoStartTime, _creatorProfile));

    // Detached

    // ARRANGE
    // Mock a detached instance by detaching it via the service
    var detachDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Weekly Yoga (Detached)"
    };

    var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

    // The DTO receives the ObjectId of the detached event, NOT the generated string format
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = detachedResult.Id.ToString(),
      StartTime = startTime.AddHours(2),
      EndTime = startTime.AddHours(3)
    };

    // ACT & ASSERT
    // This ensures your service looks up the detached event, extracts its internal RecurrencyInstanceId, 
    // realizes it is not the first occurrence, and blocks the date update.
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  #endregion

  #region Current generated

  #region first instance
  // call UpdateAllTheSequence Instead
  public async Task UpdateThisAndFollowingGeneratedFirstInstance_ShouldThrow() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var updateDto = GetUpdateDto(
      master.Id,
      startTime,
      RecurrentUpdateType.ThisAndAllFollowing,
      title: "Modified Yoga Session"
    );

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  #endregion

  #region n-th instance

  [SkippableFact]
  public async Task UpdateThisAndFollowingGeneratedNthInstance_ShouldSucceed_WithNewTitle() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var oldProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), title: "Modified Yoga Session 1");

    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(), title: "Modified Yoga Session 2", description: "Description 2");

    // 2. ACT
    var updateDto = GetUpdateDto(
      oldMaster.Id,
      startTime.AddDays(21),
      RecurrentUpdateType.ThisAndAllFollowing,
      title: "Modified Yoga Session"
    );
    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      "Weekly Yoga",
      startTime,
      1,
      startTime.AddDays(21),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(21),
      "Don't forget the mat!",
      oldProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id,
      "Modified Yoga Session",
      startTime.AddDays(21),
      1,
      oldMaster.RecurrenceEnd,
      "FREQ=WEEKLY;INTERVAL=1;",
      "Don't forget the mat!",
      oldProfileEvent,
      oldMaster.TimeZone,
      oldMaster.Id
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );

    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id,
      (
        detachedAfter.Id,
        detachedAfter.Title,
        detachedAfter.StartTime,
        detachedAfter.EndTime,
        detachedAfter.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowingGeneratedNthInstance_ShouldSucceed_WithNewDescription() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var oldProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), title: "Modified Yoga Session 1", description: "Description 1");
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(), title: "Modified Yoga Session 2", description: "Description 2");

    // Generate a valid InstanceId for the middle occurrence
    var updateDto = GetUpdateDto(
      oldMaster.Id,
      startTime.AddDays(21),
      RecurrentUpdateType.ThisAndAllFollowing,
      description: "Bring your own mat today!"
    );

    // 2. ACT
    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      "Weekly Yoga",
      startTime,
      1,
      startTime.AddDays(14),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14),
      "Don't forget the mat!",
      oldProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id.ToString(),
      "Weekly Yoga",
      startTime.AddDays(21),
      1,
      oldMaster.RecurrenceEnd,
      "FREQ=WEEKLY;INTERVAL=1",
      "Bring your own mat today!",
      oldProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        "Description 1"
      )
    );

    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedAfter.Title,
        detachedAfter.StartTime,
        detachedAfter.EndTime,
        detachedAfter.RecurrencyInstanceId!,
        "Bring your own mat today!"
      )
    );
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowingGeneratedFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var oldOldProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(14).AddHours(1), endTime: startTime.AddDays(14).AddHours(2));
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(28).AddHours(-2), endTime: startTime.AddDays(28).AddHours(-1));
    
    // 2. ACT
    var updateDto = GetUpdateDto(
      oldMaster.Id,
      startTime.AddDays(21),
      RecurrentUpdateType.ThisAndAllFollowing,
      updateStartTime: startTime.AddDays(21).AddHours(4),
      endTime: startTime.AddDays(21).AddHours(5)
    );
    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      oldMaster.Title,
      oldMaster.StartTime,
      1,
      startTime.AddDays(21),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(21),
      "Don't forget the mat!",
      oldOldProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id,
      oldMaster.Title,
      startTime.AddDays(21).AddHours(4),
      1,
      oldMaster.RecurrenceEnd,
      oldMaster.RecurrenceRule,
      "Don't forget the mat!",
      oldOldProfileEvent,
      oldMaster.TimeZone,
      oldMaster.Id
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        null
      )
    );
    
    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id
    );

    var detachedDetails2 = await _dbService.RetrieveOrNullAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detachedAfter.Id)
    );
    detachedDetails2.Should().Be(null);
  }

  #endregion

  // TODO
  // Previous with different StartDate


  // no need for recurrenceRule section because
  #endregion

  #region Current Detached

  #region first instance
  // call UpdateAllTheSequence Instead
  public async Task UpdateThisAndFollowingDetachedFirstInstance_ShouldThrow() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    // Generate a valid InstanceId for the middle occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var createDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Created Yoga Session"
    };

    var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Modified Yoga Session"
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  #endregion

  #region n-th instance

  [SkippableFact]
  public async Task UpdateThisAndFollowingDetachedNthInstance_ShouldSucceed_WithNewTitle() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var oldOldProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), title: "Modified Yoga Session 1");
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(), title: "Modified Yoga Session 2", description: "Description 2");

    // Generate cut event
    var previousDetached = await BuildDetachedEvent(startTime.AddDays(21), oldMaster.Id.ToString(), title: "Created Yoga Session");

    var previousDetachedProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileEvents,
      Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(previousDetached.Id))
    );

    // 2. ACT
    var updateDto = GetUpdateDto(
      oldMaster.Id,
      startTime,
      RecurrentUpdateType.ThisAndAllFollowing,
      title: "Modified Yoga Session"
    );
    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      oldMaster.Title,
      startTime,
      1,
      startTime.AddDays(21),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(21),
      "Don't forget the mat!",
      oldOldProfileEvent,
      oldMaster.TimeZone
    );
    
    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id,
      "Modified Yoga Session",
      startTime.AddDays(21),
      1,
      oldMaster.RecurrenceEnd,
      oldMaster.RecurrenceRule,
      "Don't forget the mat!",
      oldOldProfileEvent,
      oldMaster.TimeZone,
      oldMaster.Id
    );

    // TODO ASSERT: Detached Event
    await AssertDetached(
      previousDetached.Id.ToString(),
      newMaster.Title,
      previousDetached.StartTime,
      previousDetached.EndTime,
      newMaster.Id.ToString(),
      newMaster.StartTime.ToString("yyyyMMddTHHmmssZ"),
      "Don't forget the mat!",
      previousDetachedProfileEvent.Confirmed,
      previousDetachedProfileEvent.Role
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );

    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id,
      (
        previousDetached.Id.ToString(),
        newMaster.Title,
        previousDetached.StartTime,
        previousDetached.EndTime,
        previousDetached.RecurrencyInstanceId!,
        "Don't forget the mat!"
      ),
      (
        detachedAfter.Id,
        newMaster.Title,
        detachedAfter.StartTime,
        detachedAfter.EndTime,
        detachedAfter.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );   
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowingDetachedNthInstance_ShouldSucceed_WithNewDescription() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), title: "Modified Yoga Session 1");
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(24), oldMaster.Id.ToString(), title: "Modified Yoga Session 2", description: "Description 2");

    // Generate nth detached
    var detached = await BuildDetachedEvent(startTime.AddDays(21), oldMaster.Id.ToString(), title: "Created Yoga Session");

    // get other useful pre-acting objects
    var oldMasterProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    var detachedProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileEvents,
      Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(detached.Id))
    );

    // 2. ACT
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = oldMaster.Id.ToString(),
      InstanceId = detached.Id,
      Description = "Bring your own mat today!"
    };
    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      "Weekly Yoga",
      startTime,
      1,
      startTime.AddDays(14),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14),
      "Don't forget the mat!",
      oldMasterProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id,
      "Created Yoga Session",
      startTime.AddDays(21),
      1,
      oldMaster.RecurrenceEnd,
      oldMaster.RecurrenceRule,
      "Bring your own mat today!",
      oldMasterProfileEvent,
      oldMaster.TimeZone,
      new ObjectId(newMaster.Id)
    );

    // ASSERT: Detached Event
    await AssertDetached(detached.Id,
      newMaster.Title,
      detached.StartTime,
      detached.EndTime,
      newMaster.Id,
      newMaster.StartTime.ToString("yyyyMMddTHHmmssZ"),
      "Don't forget the mat!",
      detachedProfileEvent.Confirmed,
      detachedProfileEvent.Role
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id.ToString(),
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );

    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id,
      (
        detachedAfter.Id.ToString(),
        detachedAfter.Title,
        detachedAfter.StartTime,
        detachedAfter.EndTime,
        detachedAfter.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );
  }

  #endregion

  #region RecurrenceRule

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_WhenRecurrenceRuleFromPastEvent() {

    // Generated

    // ARRANGE
    // Start 5 days ago so it is strictly in the past day-wise
    var startTime = DateTimeOffset.UtcNow.AddDays(-5);

    var master = await BuildMasterAsync(
      "Past Standup",
      "FREQ=DAILY;COUNT=10",
      "UTC",
      startTime,
      startTime.AddHours(1)
    );

    // Generate ID for the FIRST instance (from 5 days ago)
    var recurrencyId = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{recurrencyId}";

    // Attempting to update the Time from a past instance
    var updateDtoStartTime = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = "FREQ=DAILY;COUNT=11"
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDtoStartTime, _creatorProfile));

    // Detached

    // Detach the first instance from 5 days ago (allowed since we aren't shifting AllTheSequence times yet)
    var detachDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Past Standup (Detached)"
    };
    var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

    // Attempt to update Time for AllTheSequence via the past detached instance
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = detachedResult.Id.ToString(),
      RecurrenceRule = "FREQ=DAILY;COUNT=10"
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
      _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldThrow_WhenRecurrenceRuleFromFirstInstance() {

    // Generated

    // ARRANGE
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1)
    );

    // Generate a valid InstanceId for a first occurrence (e.g., 2 weeks later)
    var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    // Attempting to update the StartTime from a non-first instance
    var updateDtoStartTime = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2"
    };

    // ACT & ASSERT
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDtoStartTime, _creatorProfile));

    // Detached

    // ARRANGE
    // Mock a detached instance by detaching it via the service
    var detachDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Weekly Yoga (Detached)"
    };

    var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

    // The DTO receives the ObjectId of the detached event, NOT the generated string format
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = detachedResult.Id.ToString(),
      RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2"
    };

    // ACT & ASSERT
    // This ensures your service looks up the detached event, extracts its internal RecurrencyInstanceId, 
    // realizes it is not the first occurrence, and blocks the date update.
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
  }



  [SkippableFact]
  public async Task UpdateGeneratedThisAndFollowingDetached_ShouldSucceed_WithRecurrencyRuleUntilUpdate() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);
    var recurrenceEnd = startTime.AddDays(43);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    //var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);
    var oldProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(14).AddHours(1), endTime: startTime.AddDays(14).AddHours(2));
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(28).AddHours(-2), endTime: startTime.AddDays(28).AddHours(-1));

    // after recurrenceEnd
    var detachedAfterRecurrenceEnd = await BuildDetachedEvent(startTime.AddDays(49), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(49).AddHours(-2), endTime: startTime.AddDays(49).AddHours(-1));


    // ACT: Cut at third occurrence (day 21)
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{oldMaster.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = oldMaster.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = $"FREQ=WEEKLY;INTERVAL=1;UNTIL={recurrenceEnd:yyyyMMdd'T'HHmmss'Z'}"
    };

    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // TODO ASSERT: Old Master
    await AssertMaster(
      oldMaster.Id.ToString(),
      oldMaster.Title,
      oldMaster.StartTime,
      1,
      startTime.AddDays(14),
      "FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14),
      "Don't forget the mat!",
      oldProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: New Master
    await AssertMaster(
      newMaster.Id.ToString(),
      newMaster.Title,
      newMaster.StartTime,
      1,
      recurrenceEnd,
      $"FREQ=WEEKLY;INTERVAL=1;UNTIL={recurrenceEnd:yyyyMMdd'T'HHmmss'Z'}",
      "Don't forget the mat!",
      oldProfileEvent,
      oldMaster.TimeZone,
      oldMaster.Id
    );

    // ASSERT: Old DetachedInstances
    await AssertDetachedInstances(
      oldMaster.Id.ToString(),
      (
        detachedBefore.Id,
        detachedBefore.Title,
        detachedBefore.StartTime,
        detachedBefore.EndTime,
        detachedBefore.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );

    // ASSERT: New DetachedInstances
    await AssertDetachedInstances(
      newMaster.Id,
      (
        detachedAfter.Id,
        detachedAfter.Title,
        detachedAfter.StartTime,
        detachedAfter.EndTime,
        detachedAfter.RecurrencyInstanceId!,
        "Don't forget the mat!"
      )
    );
  }

  // TODO UpdateGeneratedThisAndFollowingGenerated_ShouldSucceed_WithRecurrencyRuleUntilUpdate()

  // TODO
  // COUNT instead of UNTIL

  [SkippableFact]
  public async Task UpdateThisAndFollowingDetached_ShouldSucceed_WithRecurrencyRuleHeavyUpdate() {

    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var oldMaster = await BuildMasterAsync(
      "Weekly Yoga",
      "FREQ=WEEKLY;INTERVAL=1",
      "UTC",
      startTime,
      startTime.AddHours(1),
      description: "Don't forget the mat!"
    );

    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, oldMaster.Id.ToString());
    var oldMasterProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, oldMaster.Id),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    // Generate detached to cut from (day 21)
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{oldMaster.Id}_{datePart}";

    var createDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = oldMaster.Id.ToString(),
      InstanceId = instanceId,
      Title = "Detached Title"
    };

    var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

    // Generate other detached events
    var detachedBefore = await BuildDetachedEvent(startTime.AddDays(14), oldMaster.Id.ToString(), updateStartTime: startTime.AddDays(14).AddHours(1), endTime: startTime.AddDays(14).AddHours(2));
    var detachedAfter = await BuildDetachedEvent(startTime.AddDays(28), oldMaster.Id.ToString(),updateStartTime: startTime.AddDays(28).AddHours(-2), endTime: startTime.AddDays(28).AddHours(-1));

    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMaster.Id)
    );

    // ACT, update freq in rrule
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = oldMaster.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2"
    };

    var newMaster = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: NewMaster
    await AssertMaster(
      newMaster.Id,
      newMaster.Title,
      newMaster.StartTime,
      1,
      oldMaster.RecurrenceEnd,
      "FREQ=WEEKLY;INTERVAL=2",
      "Don't forget the mat!",
      oldMasterProfileEvent,
      oldMaster.TimeZone
    );

    // ASSERT: DetachedInstances Collection
    var detachedList = await _dbService.RetrieveAsync(
      CollectionName.DetachedInstances,
      Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMaster.Id)
    );
    detachedList.Should().NotBeNull();
    detachedList.MasterId.Should().Be(oldMaster.Id);

    detachedList.Instances.Count.Should().Be(0);

    var eventIds = oldDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
      CollectionName.Events,
      eventIds
    );

    detachedEvents.Count.Should().Be(0); //vengono generati dal nuovo master, che ha gli stessi valori di previousEvent(now eliminated) 
  }

  #endregion

  #endregion
}
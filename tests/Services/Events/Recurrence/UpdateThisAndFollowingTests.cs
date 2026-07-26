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

namespace Core.Tests.Services.Events.Recurrence;

[Collection("DatabaseTests")]
public class UpdateThisAndAllFollowingTests {
  private readonly ProfileService _profileService;
  private readonly RecurrentEventService _recurrentEventService;
  private readonly RecurrentEventUpdateService _recurrentUpdateService;
  private readonly MongoDbService _dbService;
  private readonly Profile _creatorProfile;

  private readonly IClientSessionHandle _session;

  public UpdateThisAndAllFollowingTests(MongoDbFixture fixture) {
    Skip.If(fixture.InitializationFailed, fixture.InitializationError);

    _dbService = fixture.DbService!;

    var scope = fixture.ServiceProvider!.CreateScope();

    _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
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

    // Generate a valid InstanceId for the first occurrence
    var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId
    };

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));

    var updateDto3 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime,
      EndTime = startTime.AddHours(1)
    };

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));
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

    // Generate a valid InstanceId for the first occurrence
    var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = ""
    };

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
    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddHours(4)
    };

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

    // Generate a valid InstanceId for the middle occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

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
  public async Task UpdateThisAndFollowingGeneratedNthInstance_ShouldSucceed_WithNewTitle() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1),
        description: "Don't forget the mat!"
    );


    var oldOldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    // Generate other detached events
    var startTime1 = startTime.AddDays(14);
    var datePart1 = startTime1.ToString("yyyyMMddTHHmmssZ");
    var instanceId1 = $"{master.Id}_{datePart1}";

    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId1,
      Title = "Modified Yoga Session 1"
    };

    var detached1 = await _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile);

    var startTime2 = startTime.AddDays(28);
    var datePart2 = startTime2.ToString("yyyyMMddTHHmmssZ");
    var instanceId2 = $"{master.Id}_{datePart2}";

    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId2,
      Title = "Modified Yoga Session 2",
      Description = "Description 2"
    };

    var detached2 = await _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile);

    // Generate a valid InstanceId for the middle occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Modified Yoga Session"
    };

    // 2. ACT
    var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);

    oldMasterEvent.Should().NotBeNull();
    oldMasterEvent.Title.Should().Be("Weekly Yoga");
    oldMasterEvent.StartTime.Should().Be(startTime);
    oldMasterEvent.EndTime.Should().Be(startTime.AddHours(1));

    oldMasterEvent.RecurrenceEnd.Should().Be(startTime.AddDays(14));
    oldMasterEvent.TimeZone.Should().Be("UTC");
    oldMasterEvent.RecurrenceRule.Should().Be("FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14));


    var oldDetails = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", oldMasterEvent.Id)
    );
    oldDetails.Should().NotBeNull();
    oldDetails.Description.Should().Be("Don't forget the mat!");

    var oldEventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, oldMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    oldEventProfile.Should().NotBeNull();
    oldEventProfile.Count.Should().Be(1);

    var oldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    oldProfileEvent.Should().NotBeNull();
    oldProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    oldProfileEvent.RecurrenceStart.Should().Be(oldMasterEvent.StartTime);
    oldProfileEvent.RecurrenceEnd.Should().Be(oldMasterEvent.RecurrenceEnd);
    oldProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: New Master
    var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

    newMasterEvent.Should().NotBeNull();
    newMasterEvent.Title.Should().Be("Modified Yoga Session");
    newMasterEvent.StartTime.Should().Be(startTime.AddDays(21));
    newMasterEvent.EndTime.Should().Be(startTime.AddDays(21).AddHours(1));
    newMasterEvent.Id.ToString().Should().NotBe(oldMasterEvent.Id.ToString());
    newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

    var details = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", newMasterEvent.Id)
    );
    details.Should().NotBeNull();
    details.Description.Should().Be("Don't forget the mat!");

    var eventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, newMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    eventProfile.Should().NotBeNull();
    eventProfile.Count.Should().Be(1);

    var masterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );
    masterProfileEvent.Should().NotBeNull();
    masterProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
    masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
    masterProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: Old DetachedInstances
    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    oldDetachedList.Should().NotBeNull();

    var oldEventIds = oldDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
        CollectionName.Events,
        oldEventIds
    );

    detachedEvents.Count.Should().Be(1);

    var oldEvent = detachedEvents.First();

    oldEvent.Title.Should().Be(detached1.Title);
    oldEvent.StartTime.Should().Be(detached1.StartTime);
    oldEvent.EndTime.Should().Be(detached1.EndTime);
    oldEvent.MasterEventId.Should().Be(oldMasterEvent.Id);
    oldEvent.RecurrencyInstanceId.Should().Be(detached1.RecurrencyInstanceId);
    oldEvent.DetachedInstance.Should().Be(true);

    var detachedDetails1 = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detached1.Id)
    );
    detachedDetails1.Description.Should().Be("Don't forget the mat!");

    var detachedInstance1 = oldDetachedList.Instances.First();
    detachedInstance1.EventId.Should().Be(new ObjectId(detached1.Id));
    detachedInstance1.RecurrencyId.Should().Be(detached1.RecurrencyInstanceId);
    detachedInstance1.StartTime.Should().Be(detached1.StartTime);

    // ASSERT: New DetachedInstances
    var newDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    newDetachedList.Should().NotBeNull();

    var newEventIds = newDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var newDetachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
        CollectionName.Events,
        newEventIds
    );
    newDetachedEvents.Count.Should().Be(1);

    var newEvent = newDetachedEvents.First();

    newEvent.Title.Should().Be(newMasterEvent.Title);
    newEvent.StartTime.Should().Be(detached2.StartTime);
    newEvent.EndTime.Should().Be(detached2.EndTime);
    newEvent.MasterEventId.Should().Be(newMasterEvent.Id);
    newEvent.RecurrencyInstanceId.Should().Be(detached2.RecurrencyInstanceId);
    newEvent.DetachedInstance.Should().Be(true);

    var detachedDetails2 = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detached2.Id)
    );
    detachedDetails2.Description.Should().Be("Don't forget the mat!");

    var detachedInstance2 = newDetachedList.Instances.First();
    detachedInstance2.EventId.Should().Be(new ObjectId(detached2.Id));
    detachedInstance2.RecurrencyId.Should().Be(detached2.RecurrencyInstanceId);
    detachedInstance2.StartTime.Should().Be(detached2.StartTime);
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowingGeneratedNthInstance_ShouldSucceed_WithNewDescription() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1),
        description: "Don't forget the mat!"
    );

    var oldOldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    // Generate other detached events
    var startTime1 = startTime.AddDays(14);
    var datePart1 = startTime1.ToString("yyyyMMddTHHmmssZ");
    var instanceId1 = $"{master.Id}_{datePart1}";

    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId1,
      Title = "Modified Yoga Session 1",
      Description = "Description 1"
    };

    var detached1 = await _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile);

    var startTime2 = startTime.AddDays(28);
    var datePart2 = startTime2.ToString("yyyyMMddTHHmmssZ");
    var instanceId2 = $"{master.Id}_{datePart2}";

    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId2,
      Title = "Modified Yoga Session 2",
      Description = "Description 2"
    };

    var detached2 = await _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile);

    // Generate a valid InstanceId for the middle occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Description = "Bring your own mat today!"
    };

    // 2. ACT
    var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);

    oldMasterEvent.Should().NotBeNull();
    oldMasterEvent.Title.Should().Be("Weekly Yoga");
    oldMasterEvent.StartTime.Should().Be(startTime);
    oldMasterEvent.EndTime.Should().Be(startTime.AddHours(1));

    oldMasterEvent.RecurrenceEnd.Should().Be(startTime.AddDays(14));
    oldMasterEvent.TimeZone.Should().Be("UTC");
    oldMasterEvent.RecurrenceRule.Should().Be("FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14));


    var oldDetails = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", oldMasterEvent.Id)
    );
    oldDetails.Should().NotBeNull();
    oldDetails.Description.Should().Be("Don't forget the mat!");

    var oldEventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, oldMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    oldEventProfile.Should().NotBeNull();
    oldEventProfile.Count.Should().Be(1);

    var oldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    oldProfileEvent.Should().NotBeNull();
    oldProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    oldProfileEvent.RecurrenceStart.Should().Be(oldMasterEvent.StartTime);
    oldProfileEvent.RecurrenceEnd.Should().Be(oldMasterEvent.RecurrenceEnd);
    oldProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: New Master
    var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

    newMasterEvent.Should().NotBeNull();
    newMasterEvent.Title.Should().Be("Weekly Yoga");
    newMasterEvent.StartTime.Should().Be(startTime.AddDays(21));
    newMasterEvent.EndTime.Should().Be(startTime.AddDays(21).AddHours(1));
    newMasterEvent.Id.ToString().Should().NotBe(oldMasterEvent.Id.ToString());
    newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

    var details = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", newMasterEvent.Id)
    );
    details.Should().NotBeNull();
    details.Description.Should().Be("Bring your own mat today!");

    var eventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, newMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    eventProfile.Should().NotBeNull();
    eventProfile.Count.Should().Be(1);

    var masterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );
    masterProfileEvent.Should().NotBeNull();
    masterProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
    masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
    masterProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: Old DetachedInstances
    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    oldDetachedList.Should().NotBeNull();

    var oldEventIds = oldDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
        CollectionName.Events,
        oldEventIds
    );

    detachedEvents.Count.Should().Be(1);

    var oldEvent = detachedEvents.First();

    oldEvent.Title.Should().Be(detached1.Title);
    oldEvent.StartTime.Should().Be(detached1.StartTime);
    oldEvent.EndTime.Should().Be(detached1.EndTime);
    oldEvent.MasterEventId.Should().Be(oldMasterEvent.Id);
    oldEvent.RecurrencyInstanceId.Should().Be(detached1.RecurrencyInstanceId);
    oldEvent.DetachedInstance.Should().Be(true);

    var detachedDetails1 = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detached1.Id)
    );
    detachedDetails1.Description.Should().Be("Description 1");

    var detachedInstance1 = oldDetachedList.Instances.First();
    detachedInstance1.EventId.Should().Be(new ObjectId(detached1.Id));
    detachedInstance1.RecurrencyId.Should().Be(detached1.RecurrencyInstanceId);
    detachedInstance1.StartTime.Should().Be(detached1.StartTime);

    // ASSERT: New DetachedInstances
    var newDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    newDetachedList.Should().NotBeNull();

    var newEventIds = newDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var newDetachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
        CollectionName.Events,
        newEventIds
    );
    newDetachedEvents.Count.Should().Be(1);

    var newEvent = newDetachedEvents.First();

    newEvent.Title.Should().Be(newMasterEvent.Title);
    newEvent.StartTime.Should().Be(detached2.StartTime);
    newEvent.EndTime.Should().Be(detached2.EndTime);
    newEvent.MasterEventId.Should().Be(newMasterEvent.Id);
    newEvent.RecurrencyInstanceId.Should().Be(detached2.RecurrencyInstanceId);
    newEvent.DetachedInstance.Should().Be(true);

    var detachedDetails2 = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detached2.Id)
    );
    detachedDetails2.Description.Should().Be("Bring your own mat today!");

    var detachedInstance2 = newDetachedList.Instances.First();
    detachedInstance2.EventId.Should().Be(new ObjectId(detached2.Id));
    detachedInstance2.RecurrencyId.Should().Be(detached2.RecurrencyInstanceId);
    detachedInstance2.StartTime.Should().Be(detached2.StartTime);
  }

  [SkippableFact]
  public async Task UpdateThisAndFollowingGeneratedFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1),
        description: "Don't forget the mat!"
    );

    var oldOldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    // Generate other detached events
    var startTime1 = startTime.AddDays(14);
    var datePart1 = startTime1.ToString("yyyyMMddTHHmmssZ");
    var instanceId1 = $"{master.Id}_{datePart1}";

    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId1,
      StartTime = startTime1.AddHours(1),
      EndTime = startTime1.AddHours(2)
    };

    var detached1 = await _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile);

    var startTime2 = startTime.AddDays(28);
    var datePart2 = startTime2.ToString("yyyyMMddTHHmmssZ");
    var instanceId2 = $"{master.Id}_{datePart2}";

    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId2,
      StartTime = startTime1.AddHours(-2),
      EndTime = startTime1.AddHours(-1),
    };

    var detached2 = await _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile);

    var oldoldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
    );

    // Generate a valid InstanceId for the middle occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      StartTime = startTime.AddDays(21).AddHours(4),
      EndTime = startTime.AddDays(21).AddHours(5)
    };

    // 2. ACT
    var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Old Master
    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);

    oldMasterEvent.Should().NotBeNull();
    oldMasterEvent.StartTime.Should().Be(startTime);
    oldMasterEvent.EndTime.Should().Be(startTime.AddHours(1));

    oldMasterEvent.RecurrenceEnd.Should().Be(startTime.AddDays(14));
    oldMasterEvent.TimeZone.Should().Be("UTC");
    oldMasterEvent.RecurrenceRule.Should().Be("FREQ=WEEKLY;INTERVAL=1;UNTIL=" + startTime.AddDays(14));

    var oldDetails = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", oldMasterEvent.Id)
    );
    oldDetails.Should().NotBeNull();
    oldDetails.Description.Should().Be("Don't forget the mat!");

    var oldEventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, oldMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    oldEventProfile.Should().NotBeNull();
    oldEventProfile.Count.Should().Be(1);

    var oldProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    oldProfileEvent.Should().NotBeNull();
    oldProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    oldProfileEvent.RecurrenceStart.Should().Be(oldMasterEvent.StartTime);
    oldProfileEvent.RecurrenceEnd.Should().Be(oldMasterEvent.RecurrenceEnd);
    oldProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: New Master
    var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

    newMasterEvent.Should().NotBeNull();
    newMasterEvent.Title.Should().Be("Weekly Yoga");
    newMasterEvent.StartTime.Should().Be(startTime.AddDays(21).AddHours(4));
    newMasterEvent.EndTime.Should().Be(startTime.AddDays(21).AddHours(5));
    newMasterEvent.Id.ToString().Should().NotBe(oldMasterEvent.Id.ToString());
    newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

    var details = await _dbService.RetrieveAsync(
        CollectionName.EventDetails,
        Builders<EventDetails>.Filter.Eq("eventId", newMasterEvent.Id)
    );
    details.Should().NotBeNull();
    details.Description.Should().Be("Don't forget the mat!");

    var eventProfile = await _dbService.RetrieveMultipleAsync(
        CollectionName.RecurrentEventProfiles,
        Builders<RecurrentEventProfile>.Filter.And(
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, newMasterEvent.Id),
            Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
        )
    );
    eventProfile.Should().NotBeNull();
    eventProfile.Count.Should().Be(1);

    var masterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );
    masterProfileEvent.Should().NotBeNull();
    masterProfileEvent.Confirmed.Should().Be(oldOldProfileEvent.Confirmed);
    masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
    masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
    masterProfileEvent.Role.Should().Be(oldOldProfileEvent.Role);

    // ASSERT: Old DetachedInstances
    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    oldDetachedList.Should().NotBeNull();

    var oldEventIds = oldDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
        CollectionName.Events,
        oldEventIds
    );

    detachedEvents.Count.Should().Be(1);

    var oldEvent = detachedEvents.First();

    oldEvent.Title.Should().Be(detached1.Title);
    oldEvent.StartTime.Should().Be(detached1.StartTime);
    oldEvent.EndTime.Should().Be(detached1.EndTime);
    oldEvent.MasterEventId.Should().Be(oldMasterEvent.Id);
    oldEvent.RecurrencyInstanceId.Should().Be(detached1.RecurrencyInstanceId);
    oldEvent.DetachedInstance.Should().Be(true);

    var detachedInstance1 = oldDetachedList.Instances.First();
    detachedInstance1.EventId.Should().Be(new ObjectId(detached1.Id));
    detachedInstance1.RecurrencyId.Should().Be(detached1.RecurrencyInstanceId);
    detachedInstance1.StartTime.Should().Be(detached1.StartTime);

    // ASSERT: New DetachedInstances
    var newDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, oldMasterEvent.Id)
    );
    newDetachedList.Should().NotBeNull();

    newDetachedList.Instances.Count.Should().Be(0);

    var detachedDetails2 = await _dbService.RetrieveOrNullAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detached2.Id)
    );
    detachedDetails2.Should().Be(null);
  }

  #endregion

  // TODO
  // COUNT instead of UNTIL
  // Previous with different StartDate

  #endregion


  #region Current Detached

  #region first instance

  #endregion

  #region n-th instance

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
  public async Task UpdateGeneratedThisAndFollowing_ShouldSucceed_WithRecurrencyRuleUntilUpdate() {
    var startTime = DateTimeOffset.UtcNow.AddHours(1);
    var recurrenceEnd = startTime.AddDays(43);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1),
        description: "Don't forget the mat!"
    );

    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);
    var oldMasterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    // Generate other detached events
    var startTime1 = startTime.AddDays(14);
    var datePart1 = startTime1.ToString("yyyyMMddTHHmmssZ");
    var instanceId1 = $"{master.Id}_{datePart1}";

    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId1,
      StartTime = startTime1.AddHours(1),
      EndTime = startTime1.AddHours(2)
    };

    var detached1 = await _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile);

    // after cut
    var startTime2 = startTime.AddDays(28);
    var datePart2 = startTime2.ToString("yyyyMMddTHHmmssZ");
    var instanceId2 = $"{master.Id}_{datePart2}";

    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId2,
      StartTime = startTime2.AddHours(-2),
      EndTime = startTime2.AddHours(-1),
    };

    var detached2 = await _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile);

    // after recurrenceEnd
    var startTime3 = startTime.AddDays(49);
    var datePart3 = startTime3.ToString("yyyyMMddTHHmmssZ");
    var instanceId3 = $"{master.Id}_{datePart3}";

    var updateDto3 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId3,
      StartTime = startTime3.AddHours(-2),
      EndTime = startTime3.AddHours(-1),
    };

    var detached3 = await _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile);


    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
    );

    // Generate a valid InstanceId for the third occurrence
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisAndAllFollowing,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = $"FREQ=WEEKLY;INTERVAL=1;UNTIL={recurrenceEnd:yyyyMMdd'T'HHmmss'Z'}"
    };

    // ACT
    var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // TODO ASSERT: Old Master


    // ASSERT: New Master
    var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

    newMasterEvent.Should().NotBeNull();
    newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
    newMasterEvent.RecurrenceEnd.Should().Be(recurrenceEnd);
    newMasterEvent.RecurrenceRule.Should().Be($"FREQ=WEEKLY;INTERVAL=1;UNTIL={recurrenceEnd:yyyyMMdd'T'HHmmss'Z'}");
    newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

    var masterProfileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );
    masterProfileEvent.Should().NotBeNull();
    masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
    masterProfileEvent.RecurrenceEnd.Should().Be(recurrenceEnd);

    // TODO ASSERT: DetachedInstances Collection

  }

  // TODO
  // COUNT instead of UNTIL

  [SkippableFact]
  public async Task UpdateThisAndFollowing_ShouldSucceed_WithRecurrencyRuleHeavyUpdate() {

    var startTime = DateTimeOffset.UtcNow.AddHours(1);

    var master = await BuildMasterAsync(
        "Weekly Yoga",
        "FREQ=WEEKLY;INTERVAL=1",
        "UTC",
        startTime,
        startTime.AddHours(1),
        description: "Don't forget the mat!"
    );

    var oldMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, master.Id);
    var oldMasterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );

    //Generate detached
    var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{master.Id}_{datePart}";

    var createDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      Title = "Detached Title"
    };

    var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

    // Generate other detached events
    var startTime1 = startTime.AddDays(14);
    var datePart1 = startTime1.ToString("yyyyMMddTHHmmssZ");
    var instanceId1 = $"{master.Id}_{datePart1}";

    var updateDto1 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId1,
      StartTime = startTime1.AddHours(1),
      EndTime = startTime1.AddHours(2)
    };

    var detached1 = await _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile);

    var startTime2 = startTime.AddDays(28);
    var datePart2 = startTime2.ToString("yyyyMMddTHHmmssZ");
    var instanceId2 = $"{master.Id}_{datePart2}";

    var updateDto2 = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId2,
      StartTime = startTime2.AddHours(-2),
      EndTime = startTime2.AddHours(-1),
    };

    var detached2 = await _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile);

    var oldDetachedList = await _dbService.RetrieveAsync(
        CollectionName.DetachedInstances,
        Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
    );

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.AllTheSequence,
      MasterEventId = master.Id.ToString(),
      InstanceId = instanceId,
      RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2"
    };

    // ACT
    var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

    // ASSERT: Master
    var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

    newMasterEvent.RecurrenceEnd.Should().Be(oldMasterEvent.RecurrenceEnd);
    newMasterEvent.RecurrenceRule.Should().Be("FREQ=WEEKLY;INTERVAL=2");
    newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

    var masterProfileEvent = await _dbService.RetrieveAsync(
        CollectionName.ProfileEvents,
        Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );
    masterProfileEvent.Should().NotBeNull();
    masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
    masterProfileEvent.RecurrenceEnd.Should().Be(oldMasterEvent.RecurrenceEnd);

    // ASSERT: DetachedInstances Collection
    var detachedList = await _dbService.RetrieveAsync(
      CollectionName.DetachedInstances,
      Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
    );
    detachedList.Should().NotBeNull();
    detachedList.MasterId.Should().Be(new ObjectId(master.Id));

    detachedList.Instances.Count.Should().Be(0);

    var eventIds = oldDetachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
      CollectionName.Events,
      eventIds
    );

    detachedEvents.Count.Should().Be(0);
  }

  #endregion

  #endregion
}
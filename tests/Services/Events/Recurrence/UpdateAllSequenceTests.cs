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
public class UpdateAllSequenceTests {
    private readonly ProfileService _profileService;
    private readonly EventService _eventService;
    private readonly RecurrentEventService _recurrentEventService;
    private readonly RecurrentEventUpdateService _recurrentUpdateService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creatorProfile;

    private readonly IClientSessionHandle _session;

    public UpdateAllSequenceTests(MongoDbFixture fixture) {
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
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenUpdateTypeIsInvalid() {
        // ARRANGE
        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence, // Wrong type for this method
            InstanceId = "any_id",
            MasterEventId = ObjectId.GenerateNewId().ToString()
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(request, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenRecurrenceRuleIsProvided() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the non-first occurrence
        var datePart = startTime.AddDays(14).ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var request = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            InstanceId = instanceId, // valid only if the first instance
            MasterEventId = master.Id.ToString(),
            RecurrenceRule = "FREQ=DAILY"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(request, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_NoUpdatesWereMade() {
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
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));

        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime,
            EndTime = startTime.AddHours(1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));

        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = null,
            EndTime = startTime.AddHours(1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto4, _creatorProfile));

        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime,
            EndTime = null
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto5, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_EmptyTitle() {
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
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = ""
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
    }

    #endregion

    #region Current generated

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenUpdatingAllSequenceFromPastGeneratedInstance() {
        // ARRANGE
        // Start 5 days ago so it's strictly in the past day-wise
        var startTime = DateTimeOffset.UtcNow.AddDays(-5);

        var master = await BuildMasterAsync(
            "Past Standup",
            "FREQ=DAILY;COUNT=10",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate ID for the instance from 5 days ago
        var recurrencyId = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{recurrencyId}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Attempted Past Update" // Just updating title to avoid the non-first date update block
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
    }
    
    #region first instance

    [SkippableFact]
    public async Task UpdateAllTheSequence_ShouldSucceed_WithNewTitle() {
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

        newMasterEvent.Should().NotBeNull();
        newMasterEvent.Title.Should().Be("Modified Yoga Session");
        newMasterEvent.StartTime.Should().Be(oldMasterEvent.StartTime);
        newMasterEvent.EndTime.Should().Be(oldMasterEvent.EndTime);
        newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
        newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", newMasterEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

        // ASSERT: RecurrentEventProfiles
        var eventProfile = await _dbService.RetrieveMultipleAsync(
            CollectionName.RecurrentEventProfiles,
            Builders<RecurrentEventProfile>.Filter.And(
                Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, newMasterEvent.Id),
                Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
            )
        );
        eventProfile.Should().NotBeNull();
        eventProfile.Count.Should().Be(1);

        // ASSERT: ProfileRecurrentEvent
        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        masterProfileEvent.Should().NotBeNull();
        masterProfileEvent.Confirmed.Should().Be(oldMasterProfileEvent.Confirmed);
        masterProfileEvent.RecurrenceStart.Should().Be(oldMasterProfileEvent.RecurrenceStart);
        masterProfileEvent.RecurrenceEnd.Should().Be(oldMasterProfileEvent.RecurrenceEnd);
        masterProfileEvent.Role.Should().Be(oldMasterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
    }

    [SkippableFact]
    public async Task UpdateAllTheSequence_ShouldSucceed_WithNewDescription() {
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

        newMasterEvent.Should().NotBeNull();
        newMasterEvent.Title.Should().Be("Weekly Yoga");
        newMasterEvent.StartTime.Should().Be(oldMasterEvent.StartTime);
        newMasterEvent.EndTime.Should().Be(oldMasterEvent.EndTime);
        newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
        newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", newMasterEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Bring your own mat today!");
    }

    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldThrow_WithWrongTime() {
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

        //(only start) start is after end
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));

        //(only start) start is equal end
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(1)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto1, _creatorProfile));

        //(only end) end is before start
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(-2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto2, _creatorProfile));

        //(only end) end is equal start
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));

        //(both set) start is after end
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(6),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto4, _creatorProfile));

        //(both set) start is equal end
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(5),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto5, _creatorProfile));

        var updateDtoNonUtcStart = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 5, 14, 0, 0, TimeSpan.FromHours(2))
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDtoNonUtcStart, _creatorProfile));

        // (only end) EndTime has a non-zero offset (e.g., UTC-5)
        var updateDtoNonUtcEnd = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = new DateTimeOffset(2026, 7, 5, 15, 0, 0, TimeSpan.FromHours(-5))
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDtoNonUtcEnd, _creatorProfile));
    }

    [SkippableFact]
    public async Task UpdateRecurrentEventInstance_ShouldThrow_WhenDatesShiftToDifferentDayInLocalTimeZone() {
        // ARRANGE
        // Using UTC+9 (Tokyo) to create a clear offset from UTC midnight.
        // NOTE: Depending on your OS/Setup, you may need to use the IANA ID "Asia/Tokyo" instead.
        var timeZoneId = "Tokyo Standard Time";

        // Base Start Time: 2026-07-05 12:00:00 UTC
        // Local Tokyo Time: 2026-07-05 21:00:00 (9:00 PM)
        var startTimeUtc = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
        var endTimeUtc = startTimeUtc.AddHours(1); // 2026-07-05 22:00:00 Local

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

        // 1. (only start) Start time shifts to the PREVIOUS day in local time.
        // Local midnight start of July 5th is July 4th 15:00:00 UTC.
        // So July 4th 14:00:00 UTC = Local July 4th 23:00:00 (Previous Day)
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 4, 14, 0, 0, TimeSpan.Zero)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto1, _creatorProfile));

        // 2. (only start) Start time shifts to the NEXT day in local time.
        // Local midnight start of July 6th is July 5th 15:00:00 UTC.
        // So July 5th 16:00:00 UTC = Local July 6th 01:00:00 (Next Day)
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 5, 16, 0, 0, TimeSpan.Zero)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto2, _creatorProfile));

        // 3. (only end) End time shifts to the PREVIOUS day in local time.
        // July 4th 14:59:00 UTC = Local July 4th 23:59:00 (Previous Day)
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = new DateTimeOffset(2026, 7, 4, 14, 59, 0, TimeSpan.Zero)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto3, _creatorProfile));

        // 4. (only end) End time shifts to the NEXT day in local time.
        // July 5th 15:30:00 UTC = Local July 6th 00:30:00 (Next Day)
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = new DateTimeOffset(2026, 7, 5, 15, 30, 0, TimeSpan.Zero)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto4, _creatorProfile));

        // 5. (both set) Both Start and End shift to the NEXT day in local time.
        // Start: Local July 6th 01:00:00 | End: Local July 6th 02:00:00
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 5, 16, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 7, 5, 17, 0, 0, TimeSpan.Zero)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto5, _creatorProfile));

        // 6. (both set) Spanning out of bounds: Starts previous day, ends next day.
        var updateDto6 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 4, 14, 0, 0, TimeSpan.Zero), // Previous Day
            EndTime = new DateTimeOffset(2026, 7, 5, 16, 0, 0, TimeSpan.Zero)    // Next Day
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto6, _creatorProfile));
    }

    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithStartTimeUpdate() {

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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(-1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

        newMasterEvent.Should().NotBeNull();
        newMasterEvent.Title.Should().Be("Weekly Yoga");
        newMasterEvent.StartTime.Should().Be(oldMasterEvent.StartTime.AddHours(-1));
        newMasterEvent.EndTime.Should().Be(oldMasterEvent.EndTime);
        newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
        newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

        // ASSERT: ProfileRecurrentEvent
        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        masterProfileEvent.Should().NotBeNull();
        masterProfileEvent.Confirmed.Should().Be(oldMasterProfileEvent.Confirmed);
        masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
        masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
        masterProfileEvent.Role.Should().Be(oldMasterProfileEvent.Role);
    }

    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithEndTimeUpdate() {
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(2)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

        newMasterEvent.Should().NotBeNull();
        newMasterEvent.Title.Should().Be("Weekly Yoga");
        newMasterEvent.StartTime.Should().Be(oldMasterEvent.StartTime);
        newMasterEvent.EndTime.Should().Be(oldMasterEvent.EndTime.AddHours(2));
        newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
        newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

        // ASSERT: ProfileRecurrentEvent
        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        masterProfileEvent.Should().NotBeNull();
        masterProfileEvent.Confirmed.Should().Be(oldMasterProfileEvent.Confirmed);
        masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
        masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
        masterProfileEvent.Role.Should().Be(oldMasterProfileEvent.Role);
    }

    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4),
            EndTime = startTime.AddHours(5)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var newMasterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, result.Id);

        newMasterEvent.Should().NotBeNull();
        newMasterEvent.Title.Should().Be("Weekly Yoga");
        newMasterEvent.StartTime.Should().Be(oldMasterEvent.StartTime.AddHours(4));
        newMasterEvent.EndTime.Should().Be(oldMasterEvent.EndTime.AddHours(4));
        newMasterEvent.Id.ToString().Should().Be(oldMasterEvent.Id.ToString());
        newMasterEvent.TimeZone.Should().Be(oldMasterEvent.TimeZone);

        // ASSERT: ProfileRecurrentEvent
        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        masterProfileEvent.Should().NotBeNull();
        masterProfileEvent.Confirmed.Should().Be(oldMasterProfileEvent.Confirmed);
        masterProfileEvent.RecurrenceStart.Should().Be(newMasterEvent.StartTime);
        masterProfileEvent.RecurrenceEnd.Should().Be(newMasterEvent.RecurrenceEnd);
        masterProfileEvent.Role.Should().Be(oldMasterProfileEvent.Role);
    }

    #endregion

    #region n-th instance

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenDatesUpdatedFromNonFirstGeneratedInstance() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate a valid InstanceId for a NON-FIRST occurrence (e.g., 2 weeks later)
        var datePart = startTime.AddDays(14).ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        // Attempting to update the StartTime from a non-first instance
        var updateDtoStartTime = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDtoStartTime, _creatorProfile));

        // Attempting to update the EndTime from a non-first instance
        var updateDtoEndTime = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(3)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDtoEndTime, _creatorProfile));
    }

    #endregion

    #endregion

    #region Current detached

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenUpdatingAllSequenceFromPastDetachedInstance() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddDays(-5);

        var master = await BuildMasterAsync(
            "Past Standup",
            "FREQ=DAILY;COUNT=10",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Generate ID for the instance from 5 days ago
        var recurrencyId = startTime.ToString("yyyyMMddTHHmmssZ");
        var generatedInstanceId = $"{master.Id}_{recurrencyId}";

        // Detach the instance from 5 days ago
        var detachDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = generatedInstanceId,
            Title = "Past Standup (Detached)"
        };
        var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

        // Attempt to update the entire sequence via the past detached instance
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = detachedResult.Id.ToString(),
            Description = "Trying to update the sequence from a past detached instance"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
    }
    
    #region first instance

    #endregion

    #region n-th instance

    [SkippableFact]
    public async Task UpdateRecurrentEvent_ShouldThrow_WhenDatesUpdatedFromNonFirstDetachedInstance() {
        // ARRANGE
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1)
        );

        // Calculate a non-first occurrence date (e.g., 1 week later)
        var nonFirstInstanceTime = startTime.AddDays(7);
        var recurrencyId = nonFirstInstanceTime.ToString("yyyyMMddTHHmmssZ");
        var generatedInstanceId = $"{master.Id}_{recurrencyId}";

        // Mock a detached instance by detaching it via the service
        var detachDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = generatedInstanceId,
            Title = "Weekly Yoga (Detached)"
        };
        
        var detachedResult = await _recurrentUpdateService.UpdateRecurrentEvent(detachDto, _creatorProfile);

        // The DTO receives the ObjectId of the detached event, NOT the generated string format
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.AllTheSequence,
            MasterEventId = master.Id.ToString(),
            InstanceId = detachedResult.Id.ToString(), 
            StartTime = nonFirstInstanceTime.AddHours(2)
        };

        // ACT & ASSERT
        // This ensures your service looks up the detached event, extracts its internal RecurrencyInstanceId, 
        // realizes it is not the first occurrence, and blocks the date update.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateRecurrentEvent(updateDto, _creatorProfile));
    }
    
    #endregion

    #endregion
}
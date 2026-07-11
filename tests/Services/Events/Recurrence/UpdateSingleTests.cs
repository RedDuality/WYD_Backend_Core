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
        
        // ARRANGE
        var request1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisAndAllFollowing, // Wrong type for this method
            InstanceId = "any_id",
            MasterEventId = ObjectId.GenerateNewId().ToString()
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(request1, _creatorProfile));
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

    #region Current generated

    #region first instance
    
    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithNewTitle() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
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
        detachedEvent.StartTime.Should().Be(master.StartTime);
        detachedEvent.EndTime.Should().Be(master.EndTime);
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

        // ASSERT: EventProfiles
        var eventProfile = await _dbService.RetrieveMultipleAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.And(
                Builders<EventProfile>.Filter.Eq(ep => ep.EventId, detachedEvent.Id),
                Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
            )
        );
        eventProfile.Should().NotBeNull();
        eventProfile.Count.Should().Be(1);

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);
        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);
        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);
    }

    [SkippableFact]
    public async Task CreateDetachedFirstInstance_ShouldSucceed_WithNewDescription() {
        // 1. ARRANGE: Create a Master Recurrent Event
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Weekly Yoga");
        detachedEvent.StartTime.Should().Be(master.StartTime);
        detachedEvent.EndTime.Should().Be(master.EndTime);
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
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
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));

        //(only start) start is equal end
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(1)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile));

        //(only end) end is before start
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(-2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile));

        //(only end) end is equal start
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile));

        //(both set) start is after end
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(6),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto4, _creatorProfile));

        //(both set) start is equal end
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(5),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto5, _creatorProfile));

        var updateDtoNonUtcStart = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = new DateTimeOffset(2026, 7, 5, 14, 0, 0, TimeSpan.FromHours(2)) 
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDtoNonUtcStart, _creatorProfile));

        // (only end) EndTime has a non-zero offset (e.g., UTC-5)
        var updateDtoNonUtcEnd = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = new DateTimeOffset(2026, 7, 5, 15, 0, 0, TimeSpan.FromHours(-5))
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDtoNonUtcEnd, _creatorProfile));

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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(-1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Weekly Yoga");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(-1));
        detachedEvent.EndTime.Should().Be(master.EndTime);

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(2)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Weekly Yoga");

        detachedEvent.StartTime.Should().Be(master.StartTime);
        detachedEvent.EndTime.Should().Be(master.EndTime.AddHours(2));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
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

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4),
            EndTime = startTime.AddHours(5)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Weekly Yoga");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(4));
        detachedEvent.EndTime.Should().Be(startTime.AddHours(5));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    #endregion

    #region n-th instance

    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithNewTitle() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
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
        detachedEvent.StartTime.Should().Be(startTime);
        detachedEvent.EndTime.Should().Be(startTime.AddHours(1));
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);
        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);
        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);
    }

    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithNewDescription() {
        // 1. ARRANGE: Create a Master Recurrent Event
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Weekly Yoga");
        detachedEvent.StartTime.Should().Be(startTime);
        detachedEvent.EndTime.Should().Be(startTime.AddHours(1));
        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Description.Should().Be("Bring your own mat today!");
    }
    
    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldThrow_WithWrongTime() {
        // ARRANGE
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1)
        );

        // Generate a valid InstanceId for the first occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        //(only start) start is after end
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));

        //(only start) start is equal end
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(1)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile));

        //(only end) end is before start
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(-2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile));

        //(only end) end is equal start
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile));

        //(both set) start is after end
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(6),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto4, _creatorProfile));

        //(both set) start is equal end
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(5),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto5, _creatorProfile));
    }
    
    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithStartTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(-1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(-1));
        detachedEvent.EndTime.Should().Be(startTime.AddHours(1));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithEndTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(2)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");

        detachedEvent.StartTime.Should().Be(startTime);
        detachedEvent.EndTime.Should().Be(startTime.AddHours(2));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task CreateDetachedNotFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4),
            EndTime = startTime.AddHours(5)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("Modified Yoga Session");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(4));
        detachedEvent.EndTime.Should().Be(startTime.AddHours(5));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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

        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id);
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    #endregion
     
    #endregion

    #region Current detacheds

    #region first instance
    
    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldSucceed_WithNewTitle() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Created Yoga Session"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);
        var previousProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(previousEvent.Id)),
                Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session"
        };
        
        // ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Id.ToString().Should().Be(previousEvent.Id.ToString());
        detachedEvent.Title.Should().Be("Modified Yoga Session");
        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);
        detachedEvent.MasterEventId.Should().Be(previousEvent.MasterEventId);
        detachedEvent.RecurrencyInstanceId.Should().Be(previousEvent.RecurrencyInstanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

        // ASSERT: EventProfiles
        var eventProfile = await _dbService.RetrieveMultipleAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.And(
                Builders<EventProfile>.Filter.Eq(ep => ep.EventId, detachedEvent.Id),
                Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
            )
        );
        eventProfile.Should().NotBeNull();
        eventProfile.Count.Should().Be(1);

        // ASSERT: EventProfiles
        var profileEvent = await _dbService.RetrieveMultipleAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, detachedEvent.Id),
                Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Count.Should().Be(1);
        
        var masterProfileEvent= await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.First().Confirmed.Should().Be(previousProfileEvent.Confirmed);
        profileEvent.First().EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.First().EventEndTime.Should().Be(detachedEvent.EndTime);
        profileEvent.First().Role.Should().Be(previousProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);
        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldSucceed_WithNewDescription() {
        // 1. ARRANGE: Create a Master Recurrent Event
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "description"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Id.ToString().Should().Be(previousEvent.Id.ToString());
        detachedEvent.Title.Should().Be(previousEvent.Title);
        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);
        detachedEvent.MasterEventId.Should().Be(previousEvent.MasterEventId);
        detachedEvent.RecurrencyInstanceId.Should().Be(previousEvent.RecurrencyInstanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Description.Should().Be("Bring your own mat today!");
    }

    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldThrow_WithWrongTime() {
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
        
        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "description"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        //(only start) start is after end
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));

        //(only start) start is equal end
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(1)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile));

        //(only end) end is before start
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(-2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile));

        //(only end) end is equal start
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile));

        //(both set) start is after end
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(6),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto4, _creatorProfile));

        //(both set) start is equal end
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(5),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto5, _creatorProfile));
    }
    
    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldSucceed_WithStartTimeUpdate() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "StartTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(-1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("StartTime update");

        detachedEvent.StartTime.Should().Be(previousEvent.StartTime.AddHours(-1));
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldSucceed_WithEndTimeUpdate() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "EndTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);


        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(2)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("EndTime update");

        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime.AddHours(2));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
        var startTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            startTime,
            startTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the first occurrence
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "BothTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4),
            EndTime = startTime.AddHours(5)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("BothTime update");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(4));
        detachedEvent.EndTime.Should().Be(startTime.AddHours(5));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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

        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id);
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }
    
    #endregion

    #region n-th instance
    
    [SkippableFact]
    public async Task UpdateDetachedNotFirstInstance_ShouldSucceed_WithNewTitle() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Created Yoga Session"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);
        var previousProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(previousEvent.Id)),
                Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "Modified Yoga Session"
        };
        
        // ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Id.ToString().Should().Be(previousEvent.Id.ToString());
        detachedEvent.Title.Should().Be("Modified Yoga Session");
        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);
        detachedEvent.MasterEventId.Should().Be(previousEvent.MasterEventId);
        detachedEvent.RecurrencyInstanceId.Should().Be(previousEvent.RecurrencyInstanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

        // ASSERT: EventProfiles
        var eventProfile = await _dbService.RetrieveMultipleAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.And(
                Builders<EventProfile>.Filter.Eq(ep => ep.EventId, detachedEvent.Id),
                Builders<EventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
            )
        );
        eventProfile.Should().NotBeNull();
        eventProfile.Count.Should().Be(1);

        // ASSERT: EventProfiles
        var profileEvent = await _dbService.RetrieveMultipleAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, detachedEvent.Id),
                Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Count.Should().Be(1);
        
        var masterProfileEvent= await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.First().Confirmed.Should().Be(previousProfileEvent.Confirmed);
        profileEvent.First().EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.First().EventEndTime.Should().Be(detachedEvent.EndTime);
        profileEvent.First().Role.Should().Be(previousProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);
        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedNotFirstInstance_ShouldSucceed_WithNewDescription() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "description"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "Bring your own mat today!"
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Id.ToString().Should().Be(previousEvent.Id.ToString());
        detachedEvent.Title.Should().Be(previousEvent.Title);
        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);
        detachedEvent.MasterEventId.Should().Be(previousEvent.MasterEventId);
        detachedEvent.RecurrencyInstanceId.Should().Be(previousEvent.RecurrencyInstanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Description.Should().Be("Bring your own mat today!");
    }

    [SkippableFact]
    public async Task UpdateDetachedNotFirstInstance_ShouldThrow_WithWrongTime() {
        // ARRANGE
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";
        
        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Description = "description"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        //(only start) start is after end
        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile));

        //(only start) start is equal end
        var updateDto1 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(1)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto1, _creatorProfile));

        //(only end) end is before start
        var updateDto2 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(-2)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto2, _creatorProfile));

        //(only end) end is equal start
        var updateDto3 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto3, _creatorProfile));

        //(both set) start is after end
        var updateDto4 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(6),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto4, _creatorProfile));

        //(both set) start is equal end
        var updateDto5 = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(5),
            EndTime = startTime.AddHours(5)
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recurrentUpdateService.UpdateSingleInstance(updateDto5, _creatorProfile));
    }
    
    [SkippableFact]
    public async Task UpdateDetachedNotFirstInstance_ShouldSucceed_WithStartTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "StartTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(-1)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("StartTime update");

        detachedEvent.StartTime.Should().Be(previousEvent.StartTime.AddHours(-1));
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime);

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedFirstNotInstance_ShouldSucceed_WithEndTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "EndTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);


        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            EndTime = startTime.AddHours(2)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("EndTime update");

        detachedEvent.StartTime.Should().Be(previousEvent.StartTime);
        detachedEvent.EndTime.Should().Be(previousEvent.EndTime.AddHours(2));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);
        
        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id );
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }

    [SkippableFact]
    public async Task UpdateDetachedNotFirstInstance_ShouldSucceed_WithBothTimeUpdate() {
        var masterStartTime = DateTimeOffset.UtcNow.AddHours(1);

        var master = await BuildMasterAsync(
            "Weekly Yoga",
            "FREQ=WEEKLY;INTERVAL=1",
            "UTC",
            masterStartTime,
            masterStartTime.AddHours(1),
            description: "Don't forget the mat!"
        );

        // Generate a valid InstanceId for the n-th occurrence
        var startTime = masterStartTime.AddDays(14);
        var datePart = startTime.ToString("yyyyMMddTHHmmssZ");
        var instanceId = $"{master.Id}_{datePart}";

        var createDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            Title = "BothTime update"
        };

        var previousEvent = await _recurrentUpdateService.UpdateSingleInstance(createDto, _creatorProfile);

        var updateDto = new UpdateRecurrentEventRequestDto {
            UpdateType = RecurrentUpdateType.ThisInstance,
            MasterEventId = master.Id.ToString(),
            InstanceId = instanceId,
            StartTime = startTime.AddHours(4),
            EndTime = startTime.AddHours(5)
        };

        // 2. ACT
        var result = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);

        // ASSERT: The Event document
        var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(CollectionName.Events, result.Id);

        detachedEvent.Should().NotBeNull();
        detachedEvent.Title.Should().Be("BothTime update");

        detachedEvent.StartTime.Should().Be(startTime.AddHours(4));
        detachedEvent.EndTime.Should().Be(startTime.AddHours(5));

        detachedEvent.MasterEventId.ToString().Should().Be(master.Id);
        detachedEvent.RecurrencyInstanceId.Should().Be(instanceId);
        detachedEvent.DetachedInstance.Should().BeTrue();

        // ASSERT: EventDetails
        var details = await _dbService.RetrieveAsync(
            CollectionName.EventDetails,
            Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
        );
        details.Should().NotBeNull();
        details.Description.Should().Be("Don't forget the mat!");

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

        var masterProfileEvent = await _dbService.RetrieveAsync(
            CollectionName.ProfileEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(master.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
            )
        );
        profileEvent.Should().NotBeNull();
        profileEvent.Confirmed.Should().Be(masterProfileEvent.Confirmed);

        profileEvent.EventStartTime.Should().Be(detachedEvent.StartTime);
        profileEvent.EventEndTime.Should().Be(detachedEvent.EndTime);

        profileEvent.Role.Should().Be(masterProfileEvent.Role);

        // ASSERT: DetachedInstances Collection
        var detachedList = await _dbService.RetrieveAsync(
            CollectionName.DetachedInstances,
            Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(master.Id))
        );
        detachedList.Should().NotBeNull();
        detachedList.MasterId.Should().Be(new ObjectId(master.Id));
        detachedList.Instances.Should().ContainSingle(i => i.RecurrencyId == instanceId);
        detachedList.Instances.Should().ContainSingle(i => i.EventId == detachedEvent.Id);
        var detachedListInstance = detachedList.Instances.First(i => i.RecurrencyId == instanceId);
        detachedListInstance.EventId.Should().Be(detachedEvent.Id);

        detachedListInstance.StartTime.Should().Be(detachedEvent.StartTime);

        var masterEventAfter = await _dbService.RetrieveAsync(
            CollectionName.RecurrentEvents,
            Builders<RecurrentEvent>.Filter.Eq(m => m.Id, new ObjectId(master.Id))
        );
        masterEventAfter.StartTime.Should().Be(master.StartTime);
        masterEventAfter.EndTime.Should().Be(master.EndTime);
    }
    
    #endregion

    #endregion
}
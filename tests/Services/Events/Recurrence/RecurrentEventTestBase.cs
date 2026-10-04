using Core.Components.Database;
using Core.DTO.EventAPI;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Model.Profiles;
using Core.Services.Events.Recurrence;
using Core.Services.Profiles;
using Core.Tests.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Core.Tests.Services.Events.Recurrence;

public abstract class RecurrentEventTestBase {

  protected readonly ProfileService _profileService;
  protected readonly RecurrentEventService _recurrentEventService;
  protected readonly RecurrentEventUpdateService _recurrentUpdateService;
  protected readonly MongoDbService _dbService;
  protected readonly Profile _creatorProfile;

  protected readonly IClientSessionHandle _session;


  protected RecurrentEventTestBase(MongoDbFixture fixture){
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

  #region build
  protected async Task<RecurrentEvent> BuildMasterAsync(
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

    var masterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, responseDto.Id);
    return masterEvent;
  }

  protected async Task<RetrieveEventResponseDto> BuildDetachedEvent(
    DateTimeOffset time,
    string masterId,
    string? title = null,
    string? description = null,
    DateTimeOffset? updateStartTime = null,
    DateTimeOffset? endTime = null) {

    var datePart = time.ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{masterId}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = RecurrentUpdateType.ThisInstance,
      MasterEventId = masterId,
      InstanceId = instanceId,
      Title = title,
      Description = description,
      StartTime = updateStartTime,
      EndTime = endTime
    };

    var detached = await _recurrentUpdateService.UpdateSingleInstance(updateDto, _creatorProfile);
    return detached;
  }

  protected static UpdateRecurrentEventRequestDto GetUpdateDto(
    ObjectId masterId,
    DateTimeOffset startTime,
    RecurrentUpdateType recurrentUpdateType,
    string? title = null,
    string? description = null,
    DateTimeOffset? updateStartTime = null,
    DateTimeOffset? endTime = null
  ){
    var datePart = startTime.AddDays(21).ToString("yyyyMMddTHHmmssZ");
    var instanceId = $"{masterId}_{datePart}";

    var updateDto = new UpdateRecurrentEventRequestDto {
      UpdateType = recurrentUpdateType,
      MasterEventId = masterId.ToString(),
      InstanceId = instanceId,
      Title = title,
      Description = description,
      StartTime = updateStartTime,
      EndTime = endTime
    };
    return updateDto;
  }

  #endregion

  #region assert
  protected async Task AssertMaster(
    string masterId, 
    string title, 
    DateTimeOffset startTime,
    double endTimeHoursOffset,
    DateTimeOffset recurrenceEnd,
    string recurrenceRule,
    string description,
    ProfileRecurrentEvent oldProfileEvent,
    TimeZoneInfo? timezone,
    ObjectId? previousMasterId = null) {

    var masterEvent = await _dbService.RetrieveByIdAsync<RecurrentEvent>(CollectionName.RecurrentEvents, masterId);

    masterEvent.Should().NotBeNull();
    masterEvent.Title.Should().Be(title);
    masterEvent.StartTime.Should().Be(startTime);
    masterEvent.EndTime.Should().Be(startTime.AddHours(endTimeHoursOffset));

    masterEvent.RecurrenceEnd.Should().Be(recurrenceEnd);
    masterEvent.TimeZone.Should().Be(timezone ?? TimeZoneInfo.Utc);
    masterEvent.RecurrenceRule.Should().Be(recurrenceRule);
    if(previousMasterId.HasValue) {
      masterEvent.Id.Should().NotBe(previousMasterId.Value);
    }


    var oldDetails = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", masterEvent.Id)
    );
    oldDetails.Should().NotBeNull();
    oldDetails.Description.Should().Be(description);

    var eventProfile = await _dbService.RetrieveMultipleAsync(
      CollectionName.RecurrentEventProfiles,
      Builders<RecurrentEventProfile>.Filter.And(
        Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, masterEvent.Id),
        Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
      )
    );
    eventProfile.Should().NotBeNull();
    eventProfile.Count.Should().Be(1);

    var profileEvent = await _dbService.RetrieveAsync(
      CollectionName.ProfileRecurrentEvents,
      Builders<ProfileRecurrentEvent>.Filter.And(
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(masterId)),
        Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
      )
    );

    profileEvent.Should().NotBeNull();
    profileEvent.Confirmed.Should().Be(oldProfileEvent.Confirmed);
    profileEvent.RecurrenceStart.Should().Be(masterEvent.StartTime);
    profileEvent.RecurrenceEnd.Should().Be(masterEvent.RecurrenceEnd);
    profileEvent.Role.Should().Be(oldProfileEvent.Role);
  }

  protected async Task AssertDetached(
    string eventId,
    string title,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    string masterEventId,
    string RecurrencyInstanceId,
    string description,
    bool confirmed,
    EventRole role
  ) {
    var detachedEvent = await _dbService.RetrieveByIdAsync<Event>(
      CollectionName.Events,
      eventId
    );

    detachedEvent.Title.Should().Be(title);
    detachedEvent.StartTime.Should().Be(startTime);
    detachedEvent.EndTime.Should().Be(endTime);
    detachedEvent.MasterEventId.Should().Be(new ObjectId(masterEventId));
    detachedEvent.RecurrencyInstanceId.Should().Be(RecurrencyInstanceId);
    detachedEvent.DetachedInstance.Should().Be(true);

    var detachedDetails = await _dbService.RetrieveAsync(
      CollectionName.EventDetails,
      Builders<EventDetails>.Filter.Eq("eventId", detachedEvent.Id)
    );
    detachedDetails.Description.Should().Be(description);

    var detachedEventProfile = await _dbService.RetrieveMultipleAsync(
      CollectionName.RecurrentEventProfiles,
      Builders<RecurrentEventProfile>.Filter.And(
        Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.EventId, new ObjectId(eventId)),
        Builders<RecurrentEventProfile>.Filter.Eq(ep => ep.ProfileId, _creatorProfile.Id)
      )
    );
    detachedEventProfile.Should().NotBeNull();
    detachedEventProfile.Count.Should().Be(1);

    var detachedProfileEvent =await _dbService.RetrieveMultipleAsync(
      CollectionName.ProfileEvents,
        Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, detachedEvent.Id),
            Builders<ProfileEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)
        )
    );
    detachedProfileEvent.Count.Should().Be(1);
    detachedProfileEvent.First().Confirmed.Should().Be(confirmed);
    detachedProfileEvent.First().EventStartTime.Should().Be(detachedEvent.StartTime);
    detachedProfileEvent.First().EventEndTime.Should().Be(detachedEvent.EndTime);
    detachedProfileEvent.First().Role.Should().Be(role);
  }
  
  protected async Task AssertDetachedInstances(
    string masterId,
    params (
      string eventId, 
      string title,
      DateTimeOffset startTime,
      DateTimeOffset endTime,
      string recurrencyInstanceId,
      string? description
      )[] expectedInstances
  ) {
    var detachedList = await _dbService.RetrieveAsync(
      CollectionName.DetachedInstances,
      Builders<DetachedInstances>.Filter.Eq(di => di.MasterId, new ObjectId(masterId))
    );
    detachedList.Should().NotBeNull();

    var oldEventIds = detachedList.Instances.Select(i => i.EventId).ToHashSet();
    var detachedEvents = await _dbService.RetrieveMultipleByIdAsync<Event>(
      CollectionName.Events,
      oldEventIds
    );

    detachedEvents.Count.Should().Be(expectedInstances.Length);

    foreach(var (eventId, title, startTime, endTime, recurrencyInstanceId, description) in expectedInstances) {
      var dbEvent = detachedEvents.First( e => e.Id == new ObjectId(eventId));
      dbEvent.Title.Should().Be(title);
      dbEvent.StartTime.Should().Be(startTime);
      dbEvent.EndTime.Should().Be(endTime);
      dbEvent.MasterEventId.Should().Be(masterId);
      dbEvent.RecurrencyInstanceId.Should().Be(recurrencyInstanceId);
      dbEvent.DetachedInstance.Should().Be(true);

      if(description != null){
        var detachedDetails = await _dbService.RetrieveAsync(
          CollectionName.EventDetails,
          Builders<EventDetails>.Filter.Eq("eventId", dbEvent.Id)
        );
        detachedDetails.Description.Should().Be(description);
      }

      var detachedInstance = detachedList.Instances.First(instance => instance.EventId == dbEvent.Id);
      detachedInstance.EventId.Should().Be(new ObjectId(eventId));
      detachedInstance.RecurrencyId.Should().Be(recurrencyInstanceId);
      detachedInstance.StartTime.Should().Be(startTime);
    }

  }
  
  #endregion

}
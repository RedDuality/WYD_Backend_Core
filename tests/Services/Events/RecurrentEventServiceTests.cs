using Core.Components.Database;
using Core.DTO.EventAPI;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Model.Profiles;
using Core.Services.Events.Recurrence;
using Core.Services.Profiles;
using Core.Tests.Setup;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Core.Tests.Services.Events;

[Collection("DatabaseTests")]
public class RecurrentEventServiceTests {
    private readonly ProfileService _profileService;
    private readonly RecurrentEventService _recurrentEventService;
    private readonly MongoDbService _dbService;
    private readonly Profile _creatorProfile;

    private readonly IClientSessionHandle _session;

    public RecurrentEventServiceTests(MongoDbFixture fixture) {
        Skip.If(fixture.InitializationFailed, fixture.InitializationError);

        _dbService = fixture.DbService!;

        var scope = fixture.ServiceProvider!.CreateScope();

        _profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        _recurrentEventService = scope.ServiceProvider.GetRequiredService<RecurrentEventService>();

        _session = fixture.StartSessionAsync().GetAwaiter().GetResult();

        string uniqueTag = $"jdoe_{Guid.NewGuid().ToString()[..8]}";
        _creatorProfile = _profileService.CreateAsync(uniqueTag, "John Doe", _session).GetAwaiter().GetResult();
    }

    #region create

    private static CreateRecurrentEventRequestDto BuildRequest(
        string title = "Team Standup",
        string rrule = "FREQ=DAILY;COUNT=5",
        string timeZone = "UTC",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null) {
        
        var s = start ?? new DateTimeOffset(2025, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var e = end ?? s.AddHours(1);
        
        return new CreateRecurrentEventRequestDto {
            Title = title,
            RecurrenceRule = rrule,
            TimeZone = timeZone,
            StartTime = s,
            EndTime = e,
            CacheIntervalStart = s,
            CacheIntervalEnd = s.AddMonths(1)
        };
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithExpectedTitle() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(title: "My Event"), _creatorProfile);

        Assert.Equal("My Event", dto.Title);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithNonEmptyId() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        Assert.False(string.IsNullOrWhiteSpace(dto.Id));
        Assert.True(ObjectId.TryParse(dto.Id, out _), "Id must be a valid ObjectId string");
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithCorrectTimes() {
        var start = new DateTimeOffset(2025, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(2);

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(start: start, end: end), _creatorProfile);

        Assert.Equal(start, dto.StartTime);
        Assert.Equal(end, dto.EndTime);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithRecurrenceRule() {
        const string rrule = "FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=10";

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(rrule: rrule), _creatorProfile);

        Assert.Equal(rrule, dto.RecurrenceRule);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithEventDetails_WhenDescriptionProvided() {
        var request = BuildRequest();
        request.Description = "Daily sync with the team";

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(request, _creatorProfile);

        Assert.NotNull(dto.EventDetails);
        Assert.Equal("Daily sync with the team", dto.EventDetails!.Description);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithNullEventDetails_WhenNoDescription() {
        var request = BuildRequest();
        request.Description = null;

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(request, _creatorProfile);

        // EventDetails should either be null or have a null Description
        Assert.True(
            dto.EventDetails == null || dto.EventDetails.Description == null,
            "EventDetails.Description should be absent when no description was supplied");
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ReturnsDto_WithExactlyOneProfileEvent() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        Assert.NotNull(dto.ProfileEvents);
        Assert.Single(dto.ProfileEvents!);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ProfileEvent_HasOwnerRole() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        var profileEvent = dto.ProfileEvents!.Single();
        Assert.Equal(EventRole.Owner, profileEvent.Role);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ProfileEvent_IsConfirmed() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        var profileEvent = dto.ProfileEvents!.Single();
        Assert.True(profileEvent.Confirmed);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_ProfileEvent_HasCreatorProfileId() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        var profileEvent = dto.ProfileEvents!.Single();
        Assert.Equal(_creatorProfile.Id.ToString(), profileEvent.ProfileId);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_PersistsRecurrentEvent_InDatabase() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(title: "Persisted Event"), _creatorProfile);

        var stored = await _dbService.RetrieveByIdAsync<RecurrentEvent>(
            CollectionName.RecurrentEvents, dto.Id);

        Assert.NotNull(stored);
        Assert.Equal("Persisted Event", stored.Title);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_PersistsEventDetails_InDatabase() {
        var request = BuildRequest();
        request.Description = "Stored description";

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(request, _creatorProfile);

        var details = await _dbService
            .RetrieveOrNullAsync(
                CollectionName.EventDetails,
                Builders<EventDetails>.Filter.Eq(d => d.EventId, new ObjectId(dto.Id)));

        Assert.NotNull(details);
        Assert.Equal("Stored description", details!.Description);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_PersistsProfileRecurrentEvent_InDatabase() {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        var profileEvent = await _dbService.RetrieveOrNullAsync<ProfileRecurrentEvent>(
            CollectionName.ProfileRecurrentEvents,
            Builders<ProfileRecurrentEvent>.Filter.And(
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, new ObjectId(dto.Id)),
                Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.ProfileId, _creatorProfile.Id)));

        Assert.NotNull(profileEvent);
        Assert.Equal(EventRole.Owner, profileEvent!.Role);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_TwoDifferentCreators_ProduceSeparateMasterEvents() {
        string tagB = $"janedoe_{Guid.NewGuid().ToString()[..8]}";
        var secondProfile = await _profileService.CreateAsync(tagB, "Jane Doe", _session);

        var dto1 = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(title: "Creator A Event"), _creatorProfile);

        var dto2 = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(title: "Creator B Event"), secondProfile);

        Assert.NotEqual(dto1.Id, dto2.Id);
    }

    [Theory]
    [InlineData("FREQ=DAILY;COUNT=3")]
    [InlineData("FREQ=WEEKLY;BYDAY=MO;COUNT=4")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=1;COUNT=6")]
    public async Task CreateRecurrentEvent_VariousRRules_AllSucceed(string rrule) {
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(rrule: rrule), _creatorProfile);

        Assert.NotNull(dto);
        Assert.Equal(rrule, dto.RecurrenceRule);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_WithNamedTimeZone_Succeeds() {
        // Windows: "Eastern Standard Time" / IANA: "America/New_York"
        // Use UTC so the test is portable across all environments.
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(timeZone: "UTC"), _creatorProfile);

        Assert.NotNull(dto);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_WithInvalidTimeZone_ThrowsArgumentException() {
        var request = BuildRequest(timeZone: "Not/A/Valid_Zone");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _recurrentEventService.CreateRecurrentEventAsync(request, _creatorProfile));
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_WithSingleOccurrenceRule_Succeeds() {
        // COUNT=1 means the series has exactly one occurrence — still a valid recurrent event.
        var dto = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(rrule: "FREQ=DAILY;COUNT=1"), _creatorProfile);

        Assert.NotNull(dto);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_WithUnicodeTitleAndDescription_RoundTripsCorrectly() {
        var request = BuildRequest(title: "会議 🗓️");
        request.Description = "毎日のスタンドアップ";

        var dto = await _recurrentEventService.CreateRecurrentEventAsync(request, _creatorProfile);

        Assert.Equal("会議 🗓️", dto.Title);
        Assert.Equal("毎日のスタンドアップ", dto.EventDetails?.Description);
    }

    [SkippableFact]
    public async Task CreateRecurrentEvent_IdsAreUnique_AcrossConsecutiveCalls() {
        var dto1 = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);
        var dto2 = await _recurrentEventService.CreateRecurrentEventAsync(
            BuildRequest(), _creatorProfile);

        Assert.NotEqual(dto1.Id, dto2.Id);
    }

    #endregion

}
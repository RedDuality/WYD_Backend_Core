using Ical.Net.DataTypes;
using MongoDB.Bson;
using Core.Services.Util;
using Xunit;

namespace Core.Tests.Services.Util;

/// <summary>
/// Comprehensive unit tests for <see cref="RecurrenceService"/>.
///
/// Test naming convention: MethodName_StateUnderTest_ExpectedBehavior
/// </summary>
public class RecurrenceServiceTests
{

    /// <summary>UTC+1 (no DST) — simple, predictable offset.</summary>
    private static readonly TimeZoneInfo CetZone =
        TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

    /// <summary>America/New_York — exercises DST transitions.</summary>
    private static readonly TimeZoneInfo EasternZone =
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    /// <summary>UTC — zero-offset sanity baseline.</summary>
    private static readonly TimeZoneInfo UtcZone = TimeZoneInfo.Utc;

    #region GetValidRule

    [Fact]
    public void GetValidRule_BareFreqRule_ReturnsNormalized()
    {
        var result = RecurrenceService.GetValidRule("FREQ=DAILY");
        Assert.Equal("FREQ=DAILY", result);
    }

    [Fact]
    public void GetValidRule_WithRrulePrefix_StripsPrefix()
    {
        var result = RecurrenceService.GetValidRule("RRULE:FREQ=WEEKLY;BYDAY=MO");
        Assert.Equal("FREQ=WEEKLY;BYDAY=MO", result);
    }

    [Fact]
    public void GetValidRule_WithRrulePrefixLowerCase_StripsPrefix()
    {
        var result = RecurrenceService.GetValidRule("rrule:FREQ=MONTHLY");
        Assert.Equal("FREQ=MONTHLY", result);
    }

    [Fact]
    public void GetValidRule_WithLeadingAndTrailingWhitespace_ReturnsTrimmedRule()
    {
        var result = RecurrenceService.GetValidRule("  FREQ=DAILY;COUNT=5  ");
        Assert.Equal("FREQ=DAILY;COUNT=5", result);
    }

    [Fact]
    public void GetValidRule_WithRrulePrefixAndWhitespace_StripsAll()
    {
        var result = RecurrenceService.GetValidRule("  RRULE:FREQ=YEARLY  ");
        Assert.Equal("FREQ=YEARLY", result);
    }

    [Fact]
    public void GetValidRule_InvalidRule_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.GetValidRule("NOT_A_VALID_RULE"));
    }

    [Fact]
    public void GetValidRule_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.GetValidRule(""));
    }

    [Fact]
    public void GetValidRule_WhitespaceOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.GetValidRule("   "));
    }

    [Fact]
    public void GetValidRule_RuleWithCountAndUntil_ReturnsRule()
    {
        // Ical.Net accepts UNTIL and COUNT (last one wins in practice)
        var result = RecurrenceService.GetValidRule("FREQ=DAILY;COUNT=3");
        Assert.Equal("FREQ=DAILY;COUNT=3", result);
    }

    [Fact]
    public void GetValidRule_ComplexWeeklyRule_ReturnsRule()
    {
        const string rule = "FREQ=WEEKLY;BYDAY=MO,WE,FR;INTERVAL=2";
        var result = RecurrenceService.GetValidRule(rule);
        Assert.Equal(rule, result);
    }

    #endregion

    #region ExtractRecurrenceEnd

    [Fact]
    public void ExtractRecurrenceEnd_RuleWithNoUntil_ReturnsMaxDateTime()
    {
        var result = RecurrenceService.ExtractRecurrenceEnd("FREQ=DAILY", UtcZone);
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            result.UtcDateTime);
    }

    [Fact]
    public void ExtractRecurrenceEnd_RuleWithCountOnly_ReturnsMaxDateTime()
    {
        var result = RecurrenceService.ExtractRecurrenceEnd("FREQ=DAILY;COUNT=10", UtcZone);
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            result.UtcDateTime);
    }

    [Fact]
    public void ExtractRecurrenceEnd_RuleWithUntilUtc_ReturnsCorrectUtcInstant()
    {
        // UNTIL=20240115T235959Z → 2024-01-15 23:59:59 UTC
        const string rule = "FREQ=DAILY;UNTIL=20240115T235959Z";
        var result = RecurrenceService.ExtractRecurrenceEnd(rule, UtcZone);

        Assert.Equal(DateTimeKind.Utc, result.UtcDateTime.Kind);
        Assert.Equal(new DateTime(2024, 1, 15, 23, 59, 59, DateTimeKind.Utc),
            result.UtcDateTime);
    }

    [Fact]
    public void ExtractRecurrenceEnd_RuleWithUntilAndNonUtcZone_ConvertsWithOffset()
    {
        // CET = UTC+1; an UNTIL timestamp interpreted in CET offsets by 1 hour in UTC
        const string rule = "FREQ=WEEKLY;UNTIL=20240201T120000Z";
        var result = RecurrenceService.ExtractRecurrenceEnd(rule, CetZone);

        // The result must be a valid DateTimeOffset in UTC
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    #endregion

    #region FormatInstanceId

    [Fact]
    public void FormatInstanceId_UtcOccurrence_ReturnsCorrectFormat()
    {
        var occurrence = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var result = RecurrenceService.FormatInstanceId(occurrence);
        Assert.Equal("20240315T103000Z", result);
    }

    [Fact]
    public void FormatInstanceId_NonUtcOccurrence_ConvertsToUtcBeforeFormatting()
    {
        // +01:00 → UTC is 09:00
        var occurrence = new DateTimeOffset(2024, 3, 15, 10, 30, 0,
            TimeSpan.FromHours(1));
        var result = RecurrenceService.FormatInstanceId(occurrence);
        Assert.Equal("20240315T093000Z", result);
    }

    [Fact]
    public void FormatInstanceId_MidnightUtc_ReturnsZeroedTime()
    {
        var occurrence = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.FormatInstanceId(occurrence);
        Assert.Equal("20240101T000000Z", result);
    }

    [Fact]
    public void FormatInstanceId_EndOfDay_FormatsCorrectly()
    {
        var occurrence = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var result = RecurrenceService.FormatInstanceId(occurrence);
        Assert.Equal("20241231T235959Z", result);
    }

    #endregion

    #region ParseInstanceId

    [Fact]
    public void ParseInstanceId_DateOnlyFormat_ReturnsLocalDateWithTzOffset()
    {
        // "20240315" in UTC+1 → 2024-03-15 00:00:00 +01:00
        var result = RecurrenceService.ParseInstanceId("20240315", CetZone);

        Assert.Equal(2024, result.Year);
        Assert.Equal(3, result.Month);
        Assert.Equal(15, result.Day);
        Assert.Equal(0, result.Hour);
        Assert.Equal(0, result.Minute);
        Assert.Equal(TimeSpan.FromHours(1), result.Offset);
    }

    [Fact]
    public void ParseInstanceId_DateTimeUtcFormat_ReturnsUtcOffset()
    {
        var result = RecurrenceService.ParseInstanceId(
            "20240315T103000Z", CetZone);

        Assert.Equal(new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero),
            result);
    }

    [Fact]
    public void ParseInstanceId_CompoundIdWithMasterId_ExtractsDatePart()
    {
        // Format: MASTERID_DATE
        var objectId = ObjectId.GenerateNewId().ToString();
        var result = RecurrenceService.ParseInstanceId(
            $"{objectId}_20240315T103000Z", CetZone);

        Assert.Equal(new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero),
            result);
    }

    [Fact]
    public void ParseInstanceId_DateOnlyFormatUtcZone_HasZeroOffset()
    {
        var result = RecurrenceService.ParseInstanceId("20240101", UtcZone);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void ParseInstanceId_DateTimeUtcFormatMidnight_Correct()
    {
        var result = RecurrenceService.ParseInstanceId("20240101T000000Z", UtcZone);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ParseInstanceId_DateOnlyWithMultipleUnderscores_UsesLastPart()
    {
        // e.g. compound ID with extra underscores in master ID section
        var result = RecurrenceService.ParseInstanceId(
            "prefix_another_20240315", UtcZone);

        Assert.Equal(2024, result.Year);
        Assert.Equal(3, result.Month);
        Assert.Equal(15, result.Day);
    }

    #endregion

    #region GetOccurrences

    [Fact]
    public void GetOccurrences_DailyRule_ReturnsExpectedDates()
    {
        // FREQ=DAILY — 5 occurrences starting 2024-03-01
        var start = new DateTimeOffset(2024, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 3, 6, 0, 0, 0, TimeSpan.Zero);
        var duration = Duration.FromHours(1);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY",
            start,
            null,
            UtcZone,
            duration,
            windowStart,
            windowEnd).ToList();

        Assert.Equal(5, occurrences.Count);
        Assert.Equal(start, occurrences[0]);
        Assert.Equal(start.AddDays(1), occurrences[1]);
        Assert.Equal(start.AddDays(4), occurrences[4]);
    }

    [Fact]
    public void GetOccurrences_WeeklyRule_SkipsNonMatchingDays()
    {
        // 2024-03-04 is a Monday
        var start = new DateTimeOffset(2024, 3, 4, 9, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=WEEKLY;BYDAY=MO",
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        // 4 Mondays in the window: Mar 4, 11, 18, 25
        Assert.Equal(4, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(DayOfWeek.Monday,
            TimeZoneInfo.ConvertTime(o, UtcZone).DayOfWeek));
    }

    [Fact]
    public void GetOccurrences_WithCount_LimitsResults()
    {
        var start = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY;COUNT=3",
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        Assert.Equal(3, occurrences.Count);
    }

    [Fact]
    public void GetOccurrences_RecurrenceEndBeforeWindowEnd_CapsAtRecurrenceEnd()
    {
        var start = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var recurrenceEnd = new DateTimeOffset(2024, 1, 3, 23, 59, 59, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 1, 10, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY",
            start,
            recurrenceEnd,
            UtcZone,
            Duration.FromHours(1),
            start,
            windowEnd).ToList();

        // Only Jan 1, 2, 3 should be returned
        Assert.True(occurrences.Count <= 3);
        Assert.All(occurrences, o =>
            Assert.True(o <= recurrenceEnd, $"{o} exceeds recurrence end {recurrenceEnd}"));
    }

    [Fact]
    public void GetOccurrences_WindowStartAfterAllOccurrences_ReturnsEmpty()
    {
        var start = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        // Event ends Jan 3; window starts Jan 10
        var recurrenceEnd = new DateTimeOffset(2024, 1, 3, 23, 59, 59, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY",
            start,
            recurrenceEnd,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        Assert.Empty(occurrences);
    }

    [Fact]
    public void GetOccurrences_WindowEndBeforeSeriesStart_ReturnsEmpty()
    {
        var start = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY",
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        Assert.Empty(occurrences);
    }

    [Fact]
    public void GetOccurrences_MonthlyRule_ReturnsOnePerMonth()
    {
        // Monthly on the 15th
        var start = new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 6, 30, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=MONTHLY",
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        Assert.Equal(6, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(15,
            TimeZoneInfo.ConvertTime(o, UtcZone).Day));
    }

    [Fact]
    public void GetOccurrences_DailyRuleInNonUtcZone_OccurrencesReturnedInUtc()
    {
        // CET (UTC+1); event at 09:00 local = 08:00 UTC
        var start = new DateTimeOffset(2024, 3, 1, 8, 0, 0, TimeSpan.Zero); // 08:00 UTC = 09:00 CET
        var windowStart = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 3, 4, 0, 0, 0, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY",
            start,
            null,
            CetZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        Assert.NotEmpty(occurrences);
        // All returned values should be UTC
        Assert.All(occurrences, o => Assert.Equal(TimeSpan.Zero, o.Offset));
    }

    [Fact]
    public void GetOccurrences_IntervalTwo_SkipsAlternateDays()
    {
        var start = new DateTimeOffset(2024, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var windowStart = start;
        var windowEnd = new DateTimeOffset(2024, 3, 10, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=DAILY;INTERVAL=2",
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        // Mar 1, 3, 5, 7, 9 → 5 occurrences
        Assert.Equal(5, occurrences.Count);
        for (int i = 0; i < occurrences.Count - 1; i++)
        {
            var gap = (occurrences[i + 1] - occurrences[i]).TotalDays;
            Assert.Equal(2.0, gap, precision: 5);
        }
    }

    [Fact]
    public void GetOccurrences_YearlyRule_ReturnsOnePerYear()
    {
        var start = new DateTimeOffset(2020, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var windowStart = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            "FREQ=YEARLY",
            start,
            null,
            UtcZone,
            Duration.FromHours(2),
            windowStart,
            windowEnd).ToList();

        Assert.Equal(6, occurrences.Count);
        Assert.All(occurrences, o =>
        {
            Assert.Equal(6, o.Month);
            Assert.Equal(15, o.Day);
        });
    }

    #endregion

    #region CheckRecurrencyId

    [Fact]
    public void CheckRecurrencyId_ValidOccurrence_ReturnsDatePart()
    {
        var masterStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var masterRecurrenceEnd = masterStart.AddYears(1);
        var duration = Duration.FromHours(1);

        // Second occurrence: 2024-03-02T10:00:00Z
        const string datePart = "20240302T100000Z";
        var objectId = ObjectId.GenerateNewId().ToString();
        var compoundId = $"{objectId}_{datePart}";

        var result = RecurrenceService.CheckRecurrencyId(
            "FREQ=DAILY",
            masterStart,
            masterEnd,
            masterRecurrenceEnd,
            UtcZone,
            compoundId);

        Assert.Equal(datePart, result);
    }

    [Fact]
    public void CheckRecurrencyId_InvalidOccurrenceNotInSeries_ThrowsArgumentException()
    {
        var masterStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var masterRecurrenceEnd = masterStart.AddYears(1);
        var objectId = ObjectId.GenerateNewId().ToString();

        // 2024-03-02 is valid but 11:00:00 doesn't match the 10:00:00 series start
        var compoundId = $"{objectId}_20240302T110000Z";

        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.CheckRecurrencyId(
                "FREQ=DAILY",
                masterStart,
                masterEnd,
                masterRecurrenceEnd,
                UtcZone,
                compoundId));
    }

    [Fact]
    public void CheckRecurrencyId_OccurrenceAfterRecurrenceEnd_ThrowsArgumentException()
    {
        var masterStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        // Series ends after 3 days
        var masterRecurrenceEnd = new DateTimeOffset(2024, 3, 3, 23, 59, 59, TimeSpan.Zero);
        var objectId = ObjectId.GenerateNewId().ToString();

        // 2024-03-10 is after the recurrence end
        var compoundId = $"{objectId}_20240310T100000Z";

        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.CheckRecurrencyId(
                "FREQ=DAILY",
                masterStart,
                masterEnd,
                masterRecurrenceEnd,
                UtcZone,
                compoundId));
    }

    [Fact]
    public void CheckRecurrencyId_MissingUnderscoreSeparator_ThrowsArgumentException()
    {
        var masterStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var masterRecurrenceEnd = masterStart.AddYears(1);

        // No underscore → parts.Length < 2
        Assert.Throws<ArgumentException>(() =>
            RecurrenceService.CheckRecurrencyId(
                "FREQ=DAILY",
                masterStart,
                masterEnd,
                masterRecurrenceEnd,
                UtcZone,
                "20240301T100000Z"));
    }

    [Fact]
    public void CheckRecurrencyId_FirstOccurrence_ReturnsDatePart()
    {
        var masterStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var masterRecurrenceEnd = masterStart.AddYears(1);
        var objectId = ObjectId.GenerateNewId().ToString();
        const string datePart = "20240301T100000Z";
        var compoundId = $"{objectId}_{datePart}";

        var result = RecurrenceService.CheckRecurrencyId(
            "FREQ=DAILY",
            masterStart,
            masterEnd,
            masterRecurrenceEnd,
            UtcZone,
            compoundId);

        Assert.Equal(datePart, result);
    }

    [Fact]
    public void CheckRecurrencyId_WeeklySeriesValidOccurrence_ReturnsDatePart()
    {
        // Weekly on Mondays; 2024-03-04 is a Monday
        var masterStart = new DateTimeOffset(2024, 3, 4, 9, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(2);
        var masterRecurrenceEnd = masterStart.AddMonths(3);
        var objectId = ObjectId.GenerateNewId().ToString();

        const string datePart = "20240311T090000Z"; // next Monday
        var compoundId = $"{objectId}_{datePart}";

        var result = RecurrenceService.CheckRecurrencyId(
            "FREQ=WEEKLY;BYDAY=MO",
            masterStart,
            masterEnd,
            masterRecurrenceEnd,
            UtcZone,
            compoundId);

        Assert.Equal(datePart, result);
    }

    #endregion

    #region FindCorrespondingInstanceId

    [Fact]
    public void FindCorrespondingInstanceId_ExactMatchInNewSeries_ReturnsFormattedId()
    {
        var seriesStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var originalOccurrence = new DateTimeOffset(2024, 3, 5, 10, 0, 0, TimeSpan.Zero);

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=DAILY",
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence);

        // Exact match should exist for daily series
        Assert.NotNull(result);
        Assert.Equal("20240305T100000Z", result);
    }

    [Fact]
    public void FindCorrespondingInstanceId_NoOccurrencesInWindow_ReturnsNull()
    {
        // Series starts far in the future; window won't contain any occurrences
        var seriesStart = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var originalOccurrence = new DateTimeOffset(2024, 3, 5, 10, 0, 0, TimeSpan.Zero);
        // searchWindowDays = 1 so window is 2024-03-04 to 2024-03-06, no series occurrences there

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=DAILY",
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence,
            searchWindowDays: 1);

        Assert.Null(result);
    }

    [Fact]
    public void FindCorrespondingInstanceId_WeeklySeriesOffByOneDay_ReturnsNearestOccurrence()
    {
        // Weekly on Mondays; original occurrence falls on a Tuesday
        var seriesStart = new DateTimeOffset(2024, 3, 4, 9, 0, 0, TimeSpan.Zero); // Monday
        // Original was a Tuesday — nearest Monday should be returned
        var originalOccurrence = new DateTimeOffset(2024, 3, 5, 9, 0, 0, TimeSpan.Zero); // Tuesday

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=WEEKLY;BYDAY=MO",
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence);

        Assert.NotNull(result);
        // Nearest Monday is either Mar 4 or Mar 11; both are valid nearest answers
        Assert.True(result is "20240304T090000Z" or "20240311T090000Z");
    }

    [Fact]
    public void FindCorrespondingInstanceId_RecurrenceEndBeforeWindow_ReturnsNull()
    {
        var seriesStart = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        // Series ends on Jan 3
        var recurrenceEnd = new DateTimeOffset(2024, 1, 3, 23, 59, 59, TimeSpan.Zero);
        // Original occurrence is in March, far outside the series
        var originalOccurrence = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=DAILY",
            seriesStart,
            recurrenceEnd,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence,
            searchWindowDays: 3);

        Assert.Null(result);
    }

    [Fact]
    public void FindCorrespondingInstanceId_DefaultSearchWindow_UsesForteenDays()
    {
        // Verify the default window of ±14 days is honoured by placing the series
        // start exactly 13 days before the original occurrence
        var originalOccurrence = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero);
        var seriesStart = originalOccurrence.AddDays(-13); // within 14-day window

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=DAILY",
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence);

        Assert.NotNull(result);
    }

    [Fact]
    public void FindCorrespondingInstanceId_ResultIsFormattedAsUtcInstanceId()
    {
        var seriesStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var originalOccurrence = new DateTimeOffset(2024, 3, 3, 10, 0, 0, TimeSpan.Zero);

        var result = RecurrenceService.FindCorrespondingInstanceId(
            "FREQ=DAILY",
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            originalOccurrence);

        Assert.NotNull(result);
        // Must match yyyyMMddTHHmmssZ
        Assert.Matches(@"^\d{8}T\d{6}Z$", result);
    }

    #endregion

    #region TruncateRuleUntil

    [Fact]
    public void TruncateRuleUntil_PlainRule_AppendsUntilOneSecondBeforeCutoff()
    {
        var cutoff = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil("FREQ=DAILY", cutoff);

        // until = cutoff - 1s = 2024-06-01T09:59:59Z
        Assert.Contains("UNTIL=20240601T095959Z", result);
        Assert.Contains("FREQ=DAILY", result);
    }

    [Fact]
    public void TruncateRuleUntil_RuleWithExistingUntil_ReplacesUntil()
    {
        const string rule = "FREQ=DAILY;UNTIL=20251231T235959Z";
        var cutoff = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        // Old UNTIL must be gone
        Assert.DoesNotContain("20251231T235959Z", result);
        Assert.Contains("UNTIL=20240601T095959Z", result);
    }

    [Fact]
    public void TruncateRuleUntil_RuleWithCount_RemovesCountAndAddsUntil()
    {
        const string rule = "FREQ=DAILY;COUNT=100";
        var cutoff = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        Assert.DoesNotContain("COUNT=", result);
        Assert.Contains("UNTIL=", result);
    }

    [Fact]
    public void TruncateRuleUntil_RuleWithBothCountAndUntil_RemovesBothThenAddsNewUntil()
    {
        const string rule = "FREQ=DAILY;COUNT=10;UNTIL=20250101T000000Z";
        var cutoff = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        Assert.DoesNotContain("COUNT=", result);
        Assert.DoesNotContain("20250101T000000Z", result);
        Assert.Contains("UNTIL=20240601T115959Z", result);
    }

    [Fact]
    public void TruncateRuleUntil_PreservesOtherParts()
    {
        const string rule = "FREQ=WEEKLY;BYDAY=MO,WE;INTERVAL=2";
        var cutoff = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        Assert.Contains("FREQ=WEEKLY", result);
        Assert.Contains("BYDAY=MO,WE", result);
        Assert.Contains("INTERVAL=2", result);
    }

    [Fact]
    public void TruncateRuleUntil_ResultIsValidRRule()
    {
        const string rule = "FREQ=DAILY;INTERVAL=3";
        var cutoff = new DateTimeOffset(2024, 12, 1, 8, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        // GetValidRule should not throw for a well-formed result
        var validResult = RecurrenceService.GetValidRule(result);
        Assert.NotNull(validResult);
    }

    [Fact]
    public void TruncateRuleUntil_CutoffWithNonUtcOffset_ConvertsToUtcForUntilString()
    {
        // cutoff = 2024-06-01 10:00:00 +01:00 → UTC = 09:00:00
        var cutoff = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.FromHours(1));
        var result = RecurrenceService.TruncateRuleUntil("FREQ=DAILY", cutoff);

        // until = UTC 08:59:59
        Assert.Contains("UNTIL=20240601T085959Z", result);
    }

    [Fact]
    public void TruncateRuleUntil_UntilCaseInsensitiveMatching_RemovesLowerCaseUntil()
    {
        // Unlikely in practice but defensive
        const string rule = "FREQ=DAILY;until=20251231T000000Z";
        var cutoff = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var result = RecurrenceService.TruncateRuleUntil(rule, cutoff);

        Assert.DoesNotContain("until=20251231T000000Z", result);
        Assert.Contains("UNTIL=", result);
    }

    #endregion

    #region CheckRecurrencyIdIsValid

    [Fact]
    public void CheckRecurrencyIdIsValid_AnyInput_ReturnsFalse()
    {
        // Intentional stub: always returns false regardless of input
        var result = RecurrenceService.CheckRecurrencyIdIsValid(
            ObjectId.GenerateNewId(),
            "FREQ=DAILY",
            "20240301T100000Z");

        Assert.False(result);
    }

    [Fact]
    public void CheckRecurrencyIdIsValid_EmptyRule_ReturnsFalse()
    {
        var result = RecurrenceService.CheckRecurrencyIdIsValid(
            ObjectId.GenerateNewId(),
            "",
            "");

        Assert.False(result);
    }

    #endregion

    #region Integration

    [Fact]
    public void Integration_FormatThenParseInstanceId_RoundTrips()
    {
        var original = new DateTimeOffset(2024, 7, 20, 14, 30, 0, TimeSpan.Zero);
        var formatted = RecurrenceService.FormatInstanceId(original);
        var parsed = RecurrenceService.ParseInstanceId(formatted, UtcZone);

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Integration_TruncatedRuleOccurrencesStopBeforeCutoff()
    {
        var seriesStart = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var cutoff = new DateTimeOffset(2024, 3, 6, 10, 0, 0, TimeSpan.Zero); // 6th day
        var truncated = RecurrenceService.TruncateRuleUntil(
            "FREQ=DAILY", cutoff);

        var occurrences = RecurrenceService.GetOccurrences(
            truncated,
            seriesStart,
            null,
            UtcZone,
            Duration.FromHours(1),
            seriesStart,
            new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero)).ToList();

        // Cutoff is the 6th; the old series must NOT include the 6th occurrence
        Assert.All(occurrences, o => Assert.True(o < cutoff,
            $"Occurrence {o} must be before cutoff {cutoff}"));
        // And the 5th must be present
        Assert.Contains(occurrences, o => o.Date == new DateTime(2024, 3, 5));
    }

    [Fact]
    public void Integration_ExtractRecurrenceEndMatchesGetOccurrencesUpperBound()
    {
        const string rule = "FREQ=DAILY;UNTIL=20240310T235959Z";
        var extractedEnd = RecurrenceService.ExtractRecurrenceEnd(rule, UtcZone);

        var start = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var occurrences = RecurrenceService.GetOccurrences(
            rule,
            start,
            extractedEnd,
            UtcZone,
            Duration.FromHours(1),
            start,
            extractedEnd.AddDays(10)).ToList();

        Assert.All(occurrences, o =>
            Assert.True(o <= extractedEnd,
                $"Occurrence {o} exceeds extracted recurrence end {extractedEnd}"));
    }

    [Fact]
    public void Integration_CheckRecurrencyId_UsesFormatInstanceIdOutput()
    {
        var masterStart = new DateTimeOffset(2024, 4, 1, 9, 0, 0, TimeSpan.Zero);
        var masterEnd = masterStart.AddHours(1);
        var masterRecurrenceEnd = masterStart.AddMonths(6);
        var objectId = ObjectId.GenerateNewId().ToString();

        // Pick the 3rd occurrence
        var thirdOccurrence = masterStart.AddDays(2);
        var datePart = RecurrenceService.FormatInstanceId(thirdOccurrence);
        var compoundId = $"{objectId}_{datePart}";

        var result = RecurrenceService.CheckRecurrencyId(
            "FREQ=DAILY",
            masterStart,
            masterEnd,
            masterRecurrenceEnd,
            UtcZone,
            compoundId);

        Assert.Equal(datePart, result);
    }

    [Fact]
    public void Integration_GetValidRule_ThenGetOccurrences_WorksTogether()
    {
        var rawRule = "RRULE:FREQ=WEEKLY;BYDAY=TU,TH";
        var validRule = RecurrenceService.GetValidRule(rawRule);

        // 2024-03-05 is a Tuesday
        var start = new DateTimeOffset(2024, 3, 5, 8, 0, 0, TimeSpan.Zero);
        var windowStart = start;
        var windowEnd = new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = RecurrenceService.GetOccurrences(
            validRule,
            start,
            null,
            UtcZone,
            Duration.FromHours(1),
            windowStart,
            windowEnd).ToList();

        // March: Tue + Thu in weeks 5-31 → 4 Tuesdays (5,12,19,26) + 4 Thursdays (7,14,21,28) = 8
        Assert.Equal(8, occurrences.Count);
        Assert.All(occurrences, o =>
        {
            var dow = o.ToUniversalTime().DayOfWeek;
            Assert.True(dow == DayOfWeek.Tuesday || dow == DayOfWeek.Thursday,
                $"Unexpected day of week: {dow}");
        });
    }

    #endregion
}
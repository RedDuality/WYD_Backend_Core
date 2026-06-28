using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using MongoDB.Bson;

namespace Core.Services.Util;

public class RecurrenceService() {
    public static IEnumerable<DateTimeOffset> GetOccurrences(
        string recurrenceRule,
        DateTimeOffset startTime,
        DateTimeOffset? recurrenceEnd,
        TimeZoneInfo tz,
        Duration singleEventDuration,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd) {
        // Cap the upper bound at the series' own recurrence end, if defined.
        var effectiveEnd = recurrenceEnd.HasValue && recurrenceEnd.Value < windowEnd
            ? recurrenceEnd.Value
            : windowEnd;

        // Ical.Net works in local DateTime; convert from UTC using the event time zone.
        var dtStartLocal = TimeZoneInfo.ConvertTime(startTime, tz).DateTime;
        var searchStartLocal = TimeZoneInfo.ConvertTime(windowStart, tz).DateTime;
        var searchEndLocal = TimeZoneInfo.ConvertTime(effectiveEnd, tz).DateTime;

        var calEvent = new CalendarEvent {
            DtStart = new CalDateTime(dtStartLocal, tz.Id),
            Duration = singleEventDuration,
            RecurrenceRule = new(recurrenceRule)
        };


        var calendar = new Ical.Net.Calendar();
        calendar.Events.Add(calEvent);


        return calendar
            .GetOccurrences(new CalDateTime(searchStartLocal, tz.Id))
            .Where(o => o.Period.StartTime.Value <= searchEndLocal)
            .Select(o => {
                var localDt = o.Period.StartTime.Value;
                var offset = tz.GetUtcOffset(localDt);
                return new DateTimeOffset(
                    DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified), offset)
                    .ToUniversalTime();
            });
    }



    #region rule
    public static string GetValidRule(string rule) {
        var normalizedRule = rule.Trim();
        const string prefix = "RRULE:";

        if (normalizedRule.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalizedRule = normalizedRule[prefix.Length..];

        if (IsValidRRule(normalizedRule))
            return normalizedRule;

        throw new ArgumentException($"Invalid recurrence rule: '{normalizedRule}'.");
    }

    private static bool IsValidRRule(string rule) {
        if (string.IsNullOrWhiteSpace(rule)) return false;
        try {
            var _ = new RecurrencePattern(rule);
            return true;
        }
        catch {
            return false;
        }
    }


    public static DateTimeOffset ExtractRecurrenceEnd(string recurrenceRule, TimeZoneInfo tz) {
        var pattern = new RecurrencePattern(recurrenceRule);

        if (pattern.Until == null)
            return new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var dt = pattern.Until.Value;

        var offset = tz.GetUtcOffset(dt);
        var dto = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), offset);

        return dto.ToUniversalTime();
    }

    /// <summary>
    /// Returns a new recurrence rule string that is identical to <paramref name="recurrenceRule"/>
    /// except that any existing UNTIL/COUNT clause is replaced by an UNTIL set to one second
    /// before <paramref name="cutoff"/>. The cutoff occurrence itself therefore belongs to the
    /// new master, not to the old one.
    /// </summary>
    public static string TruncateRuleUntil(string recurrenceRule, DateTimeOffset cutoff) {
        var untilStr = cutoff.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");

        var parts = recurrenceRule
            .Split(';')
            .Where(p => !p.StartsWith("UNTIL=", StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("COUNT=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        parts.Add($"UNTIL={untilStr}");

        return string.Join(";", parts);
    }

    #endregion

    #region recurrencyId

    public static string FormatInstanceId(DateTimeOffset occurrence)
        => occurrence.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");


    public static bool CheckRecurrencyIdIsValid(ObjectId masterId, string recurrenceRule, string recurrencyId) {
        // TODO
        return false;
    }

    /// <summary>
    /// Verifies if a compound ID represents a valid occurrence in the master's sequence.
    /// Returns the extracted date part (RecurrencyInstanceId) if valid.
    /// </summary>
    public static string CheckRecurrencyId(
        string masterRecurrenceRule,
        DateTimeOffset masterStartTime,
        DateTimeOffset masterEndTime,
        DateTimeOffset masterRecurrenceEnd,
        TimeZoneInfo timeZone,
        string compoundId
    ) {
        var datePart = string.Empty;

        try {
            var parts = compoundId.Split('_');
            if (parts.Length < 2) throw new Exception();

            datePart = parts.Last();

            DateTimeOffset occurrenceStart = ParseInstanceId(datePart, timeZone);
            TimeSpan duration = masterEndTime - masterStartTime;

            // Check if this specific occurrence exists in the rule
            // We use a 1-second window to check for the exact start time
            var occurenceExists = GetOccurrences(
                masterRecurrenceRule,
                masterStartTime,
                masterRecurrenceEnd,
                timeZone,
                Duration.FromTimeSpanExact(duration),
                occurrenceStart,
                occurrenceStart.AddSeconds(1)
            ).Any(o => o.Equals(occurrenceStart));

            if (!occurenceExists) throw new Exception();
        }
        catch {
            throw new ArgumentException("The provided InstanceEventId is not a valid occurrence of this series.");
        }

        return datePart;
    }

    /// DATE format:      yyyyMMdd         → interpreted in the event's local time zone
    /// DATE-TIME format: yyyyMMddTHHmmssZ → UTC instant
    /// 
    /// instanceId = MASTERID_DATE
    public static DateTimeOffset ParseInstanceId(string instanceId, TimeZoneInfo timeZone) {
        var parts = instanceId.Split("_");
        var dateString = parts.Last();

        if (dateString.Length == 8)
        {
            var date = DateTime.ParseExact(
                dateString,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None);

            return new DateTimeOffset(date, timeZone.GetUtcOffset(date));
        }
        else
        {
            var utcDt = DateTime.ParseExact(
                dateString,
                "yyyyMMddTHHmmssZ",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

            return new DateTimeOffset(utcDt, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Given an old occurrence time (from a detached instance's recurrencyId),
    /// finds the nearest occurrence in the new recurrence series and returns
    /// its formatted instance id, or null if none is found within the search window.
    /// </summary>
    public static string? FindCorrespondingInstanceId(
        string newRecurrenceRule,
        DateTimeOffset newSeriesStart,
        DateTimeOffset? newRecurrenceEnd,
        TimeZoneInfo tz,
        Duration singleEventDuration,
        DateTimeOffset originalOccurrenceTime,
        int searchWindowDays = 14) {
            
        var windowStart = originalOccurrenceTime.AddDays(-searchWindowDays);
        var windowEnd = originalOccurrenceTime.AddDays(searchWindowDays);

        var closest = GetOccurrences(
                newRecurrenceRule, newSeriesStart, newRecurrenceEnd,
                tz, singleEventDuration, windowStart, windowEnd)
            .OrderBy(o => Math.Abs((o - originalOccurrenceTime).Ticks))
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();

        return closest.HasValue ? FormatInstanceId(closest.Value) : null;
    }
    #endregion

}

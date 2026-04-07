using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace Core.Services.Util;

public class RecurrenceService()
{
    public static IEnumerable<DateTimeOffset> GetOccurrences(
        string recurrenceRule,
        DateTimeOffset startTime,
        DateTimeOffset? recurrenceEnd,
        TimeZoneInfo tz,
        Duration singleEventDuration,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        // Cap the upper bound at the series' own recurrence end, if defined.
        var effectiveEnd = recurrenceEnd.HasValue && recurrenceEnd.Value < windowEnd
            ? recurrenceEnd.Value
            : windowEnd;

        // Ical.Net works in local DateTime; convert from UTC using the event time zone.
        var dtStartLocal = TimeZoneInfo.ConvertTime(startTime, tz).DateTime;
        var searchStartLocal = TimeZoneInfo.ConvertTime(windowStart, tz).DateTime;
        var searchEndLocal = TimeZoneInfo.ConvertTime(effectiveEnd, tz).DateTime;

        var calEvent = new CalendarEvent
        {
            DtStart = new CalDateTime(dtStartLocal, tz.Id),
            Duration = singleEventDuration,
            RecurrenceRules =
            [
                new(recurrenceRule)
            ]
        };


        var calendar = new Ical.Net.Calendar();
        calendar.Events.Add(calEvent);


        return calendar
            .GetOccurrences(new CalDateTime(searchStartLocal, tz.Id))
            .Where(o => o.Period.StartTime.Value <= searchEndLocal)
            .Select(o =>
            {
                var localDt = o.Period.StartTime.Value;
                var offset = tz.GetUtcOffset(localDt);
                return new DateTimeOffset(
                    DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified), offset)
                    .ToUniversalTime();
            });
    }

    public static DateTimeOffset ExtractRecurrenceEnd(string recurrenceRule, TimeZoneInfo tz)
    {
        var pattern = new RecurrencePattern(recurrenceRule);

        if (pattern.Until == null)
            return new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var dt = pattern.Until.Value;

        var offset = tz.GetUtcOffset(dt);
        var dto = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), offset);

        return dto.ToUniversalTime();
    }

    public static string GetValidRule(string rule)
    {
        var normalizedRule = rule.Trim();
        const string prefix = "RRULE:";

        if (normalizedRule.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalizedRule = normalizedRule[prefix.Length..];

        if (IsValidRRule(normalizedRule))
            return normalizedRule;

        throw new ArgumentException($"Invalid recurrence rule: '{normalizedRule}'.");
    }

    private static bool IsValidRRule(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule)) return false;
        try
        {
            var _ = new RecurrencePattern(rule); // if you use Ical.Net
            return true;
        }
        catch
        {
            return false;
        }
    }

}

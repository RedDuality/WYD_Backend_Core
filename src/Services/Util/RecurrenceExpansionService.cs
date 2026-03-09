
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace Core.Services.Util;

public class RecurrenceExpansionService()
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


        var calendar = new Calendar();
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
}

using System.Globalization;

namespace Core.External.ImportPlatform.GoogleCalendar;

public class GoogleCalendarParserService()
{
    private static DateTimeOffset ExtractRecurrenceEnd(string rruleString)
    {
        if (string.IsNullOrEmpty(rruleString))
            return DateTimeOffset.MaxValue; // No end = infinite

        var until = rruleString
            .Split('\n')
            .Select(line => line.Split("UNTIL="))
            .Where(parts => parts.Length == 2)
            .Select(parts => parts[1].Split(';')[0])
            .FirstOrDefault();

        if (until != null &&
            DateTime.TryParseExact(until,
                ["yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }

        var count = rruleString
            .Split('\n')
            .Select(line => line.Split("COUNT="))
            .Where(parts => parts.Length == 2)
            .Select(parts => parts[1].Split(';')[0])
            .FirstOrDefault();

        // For COUNT, you'd need to calculate: start + (interval * count)
        // For now, return MaxValue to be safe
        return DateTimeOffset.MaxValue;
    }

    private static TimeZoneInfo ResolveTimeZone(GoogleCalendarEvent e)
    {
        var ianaId = e.Start.TimeZone ?? e.End.TimeZone;
        if (ianaId is null)
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }


    private static (DateTimeOffset start, DateTimeOffset end, bool isAllDay)
        ParseTimes(GoogleCalendarEvent e)
    {
        var isAllDay = e.Start.Date is not null;

        if (isAllDay)
        {
            var startDate = DateOnly.Parse(e.Start.Date!).ToDateTime(TimeOnly.MinValue);
            var endDateExclusive = DateOnly.Parse(e.End.Date!).ToDateTime(TimeOnly.MinValue);

            var endDate = endDateExclusive.AddDays(-1);

            return (
                new DateTimeOffset(startDate, TimeSpan.Zero),
                new DateTimeOffset(endDate, TimeSpan.Zero),
                true
            );
        }

        var start = e.Start.DateTime!.Value.ToUniversalTime();
        var end = e.End.DateTime!.Value.ToUniversalTime();

        return (
            new DateTimeOffset(start),
            new DateTimeOffset(end),
            false
        );
    }


    /// <summary>
    /// Maps a master recurring Google Calendar event to a <see cref="RecurrentEvent"/>.
    /// </summary>
    public static ImportRecurrentEventDto ToRecurrentEvent(GoogleCalendarEvent e, string accountUid)
    {
        var (start, end, isAllDay) = ParseTimes(e);
        var timeZone = ResolveTimeZone(e);

        var rrule = e.Recurrence is { Count: > 0 }
            ? string.Join("\n", e.Recurrence)
            : string.Empty;

        return new ImportRecurrentEventDto(
            Title: e.Summary ?? "(No title)",
            StartTime: start,
            EndTime: end,
            TimeZone: timeZone,
            ImportedAccountUid: accountUid,
            RecurrenceRule: rrule,
            ExternalEventId: e.Id
        )
        {
            Description = e.Description,
            IsAllDay = isAllDay,
            RecurrenceEnd = ExtractRecurrenceEnd(rrule)
        };
    }

    public static ImportEventDto MapSingle(GoogleCalendarEvent e, string accountUid)
    {
        var (start, end, isAllDay) = ParseTimes(e);

        return new ImportEventDto(
            Title: e.Summary ?? "(No title)",
            StartTime: start,
            EndTime: end,
            ImportedAccountUid: accountUid,
            ExternalEventId: e.Id
        )
        {
            Description = e.Description,
            IsAllDay = isAllDay,
            ExternalMasterEventId = e.RecurringEventId
        };
    }
}
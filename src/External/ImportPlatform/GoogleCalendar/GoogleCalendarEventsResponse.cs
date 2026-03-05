
using System.Text.Json.Serialization;

namespace Core.External.ImportPlatform.GoogleCalendar;

public record GoogleCalendarEventsResponse(
    [property: JsonPropertyName("items")] List<GoogleCalendarEvent> Items,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken
);

public record GoogleCalendarEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("start")] GoogleCalendarDateTime Start,
    [property: JsonPropertyName("end")] GoogleCalendarDateTime End,

    // Present on master recurring events (e.g. ["RRULE:FREQ=WEEKLY;BYDAY=MO"]).
    // Null for non-recurring events and for exception instances.
    [property: JsonPropertyName("recurrence")] List<string>? Recurrence,

    // Present on exception instances (modified/single occurrences of a recurring series).
    // Contains the id of the master recurring event.
    [property: JsonPropertyName("recurringEventId")] string? RecurringEventId
);

// Google uses "date" for all-day events and "dateTime" for timed events.
// "timeZone" is present on timed events and carries the IANA zone name (e.g. "America/New_York").
public record GoogleCalendarDateTime(
    [property: JsonPropertyName("dateTime")] DateTime? DateTime,
    [property: JsonPropertyName("date")] string? Date,          // "yyyy-MM-dd"
    [property: JsonPropertyName("timeZone")] string? TimeZone   // IANA time-zone name
);


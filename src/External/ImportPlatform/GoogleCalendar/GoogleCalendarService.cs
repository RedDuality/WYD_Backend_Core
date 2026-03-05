using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Model.Events;
using Core.Model.Profiles;

namespace Core.External.ImportPlatform.GoogleCalendar;

public class GoogleCalendarService(
    ImportService importService
)
{
    public static async Task<string> GetEmailFromIdToken(string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Invalid or expired Google access token.");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accountEmail = json.GetProperty("email").GetString()!;

        return accountEmail;
    }

    public async Task FetchEventsAsync(
            string accessToken,
            string accountUid,
            Profile profile,
            CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var recurrentEvents = new List<ImportRecurrentEventDto>();
        var events = new List<ImportEventDto>();
        string? pageToken = null;

        do
        {
            var url = BuildPageUrl(pageToken);
            var response = await http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Google Calendar API returned {(int)response.StatusCode}: {body}");
            }

            var page = await response.Content
                .ReadFromJsonAsync<GoogleCalendarEventsResponse>(ct)
                ?? throw new InvalidOperationException(
                    "Google Calendar API returned an empty response.");

            foreach (var item in page.Items)
            {
                // Skip cancelled (deleted) events — handle deletions separately
                if (item.Status == "cancelled") continue;

                if (item.Recurrence is { Count: > 0 })
                {
                    recurrentEvents.Add(GoogleCalendarParserService.ToRecurrentEvent(item, accountUid));
                }
                else
                {
                    events.Add(GoogleCalendarParserService.MapSingle(item, accountUid));
                }
            }
            await importService.SaveMultipleEvents(events, recurrentEvents, profile);
            pageToken = page.NextPageToken;

        } while (pageToken is not null);

    }

    /// <summary>
    /// singleEvents=false returns master recurring events together with any
    /// exception instances (modified occurrences). orderBy is omitted because
    /// orderBy=startTime is only valid when singleEvents=true.
    /// </summary>
    private static string BuildPageUrl(string? pageToken)
    {
        const string CalendarBaseUrl =
            "https://www.googleapis.com/calendar/v3/calendars/primary/events";

        var url = $"{CalendarBaseUrl}?singleEvents=false&maxResults=250";
        if (pageToken is not null)
            url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        return url;
    }

    

}
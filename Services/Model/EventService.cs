using MongoDB.Driver;
using MongoDB.Bson;
using Core.Services.Util;
using Core.Model;

namespace Core.Services.Model;


public class EventService(MongoDbService dbService, ProfileEventService profileEventService )//ServiceBusService sbs)
{
    private readonly string collectionName = "Events";

    public async Task<Event> CreateEventAsync(Event newEvent, string profileId)
    {
        Event Event = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var createdEvent = await dbService.CreateOneAsync(collectionName, newEvent, session);
            var newProfileEvent = new ProfileEvent
            (
                createdEvent, new ObjectId(profileId)
            );
            await profileEventService.CreateProfileEventAsync(newProfileEvent, session);

            return createdEvent;
        });

        return Event;
    }

    public async Task<Event> RetrieveEventById(string id)
    {
        return await dbService.RetrieveByIdAsync<Event>(collectionName, id);
    }

/*
    public async Task<List<EventDto>> RetrieveEventsByProfileId(string profileId, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var aggregate = dbService.GetAggregate<ProfileEvent>("ProfileEvents");

        // Step 1: Match ProfileEvents by profileId and time range
        var matchStage = aggregate
            .Match(pe =>
                pe.ProfileId == new ObjectId(profileId) &&
                pe.EventStartTime <= endTime.ToUniversalTime() &&
                pe.EventEndTime >= startTime.ToUniversalTime())
            .Limit(40);

        // Step 2: Lookup the corresponding Event for each ProfileEvent
        var lookupStage = matchStage.Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
            dbService.GetCollection<Event>(collectionName),
            pe => pe.EventId,
            e => e.Id,
            pe => pe.Events);

        var projected = lookupStage
            .Project(pe => new
            {
                Event = pe.Events[0],
                pe.ProfileId,
                pe.Role,
                pe.Confirmed
            });

        var intermediateResults = await projected.ToListAsync();

        var result = intermediateResults.Select(pe => new EventDto
        {
            Hash = pe.Event.Id.ToString(),
            Title = pe.Event.Title,
            Description = pe.Event.Description,
            StartTime = pe.Event.StartTime,
            EndTime = pe.Event.EndTime,
            ProfileEvents =
            [
                new ProfileEventDto {
                    ProfileHash = pe.ProfileId.ToString(),
                    Role = pe.Role,
                    Confirmed = pe.Confirmed,
                    Trusted = false
                }
            ]
        }).ToList();

        //var result = await projectStage.ToListAsync();


        return result;

    }



    public async Task ShareEventAsync(String eventId, List<string> profileIds)
    {
        var ev = await RetrieveEventById(eventId);
        await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            //TODO make this without the for
            foreach (var profileId in profileIds)
            {
                var newProfileEvent = new ProfileEvent
                (
                     ev, new ObjectId(profileId)
                );
                await profileEventService.CreateProfileEventAsync(newProfileEvent, session);
            }

            return true;
        });

    }

    public async Task<Event> UpdateEventById(string id, string title)
    {
        Event updatedEvent = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var updateDefinition = Builders<Event>.Update
                .Set(e => e.Title, title)
                .Set(e => e.UpdatedAt, DateTimeOffset.UtcNow);

            var updatedEvent = await dbService.PatchUpdateAsync(collectionName, id, updateDefinition, session);

            var updateNotification = new EventUpdateQueueMessage(updatedEvent.Id.ToString())
            {
                UpdatedAt = updatedEvent.UpdatedAt
            };
            await sbs.SendMessageAsync("eventupdate", updateNotification);

            return updatedEvent;
        });
        return updatedEvent;

    }
*/
}
using MongoDB.Bson;
using Core.Model;
using Core.Model.Join;
using Core.Components.Database;
using Core.Model.Dto;
using MongoDB.Driver;
using Core.Model.Util;

namespace Core.Services.Model;


public class EventService(MongoDbService dbService, ProfileEventService profileEventService)//ServiceBusService sbs)
{
    private readonly CollectionName eventCollection = CollectionName.Events;

    public async Task<Event> CreateEventAsync(EventDto newEventDto, string profileId)
    {
        var newEvent = new Event
        {
            Title = newEventDto.Title!,
            Description = newEventDto.Description,
            StartTime = newEventDto.StartTime.ToUniversalTime(),
            EndTime = newEventDto.EndTime.ToUniversalTime(),
            // Add other properties as needed from EventDto to Event
        };

        Event Event = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var createdEvent = await dbService.CreateOneAsync(eventCollection, newEvent, session);
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
        return await dbService.RetrieveByIdAsync<Event>(eventCollection, id);
    }

    public async Task<List<EventDto>> RetrieveEventsByProfileId(List<string> profileHashes, DateTimeOffset startTime, DateTimeOffset? endTime)
    {
        var objectIds = profileHashes.Select(ph => new ObjectId(ph)).ToList();

        var filterBuilder = Builders<ProfileEvent>.Filter;
        var filters = new List<FilterDefinition<ProfileEvent>>
        {
            filterBuilder.In(pe => pe.ProfileId, objectIds),
            filterBuilder.Gte(pe => pe.EventEndTime, startTime.ToUniversalTime())
        };

        if (endTime.HasValue)
        {
            filters.Add(filterBuilder.Lte(pe => pe.EventStartTime, endTime.Value.ToUniversalTime()));
        }

        var filter = filterBuilder.And(filters);

        // Define the aggregation pipeline
        var pipeline = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents)
            .Match(filter)
            .Limit(40)
            .Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
                dbService.GetCollection<Event>(eventCollection),
                pe => pe.EventId,
                e => e.Id,
                pe => pe.Events)
            .Unwind<ProfileEventWithCorrespondingEvents, ProfileEventWithCorrespondingEvents>(pe => pe.Events);

        // Use a Group stage to combine profile events for the same event.
        // The result type is now explicitly defined as AggregatedEventWithProfileEvents.
        var groupedResult = pipeline
            .Group(
                pe => pe.Events.First(), // Grouping by the Event document itself
                g => new AggregatedEventWithProfileEvents
                {
                    Event = g.Key,
                    ProfileEvents = g.Select(pe => new ProfileEvent(g.Key, pe.ProfileId)).ToList()
                });

        // The final projection from the grouped result to the EventDto
        var projectedResults = groupedResult
            .Project(g => new EventDto
            {
                Hash = g.Event.Id.ToString(),
                Title = g.Event.Title,
                Description = g.Event.Description,
                StartTime = g.Event.StartTime,
                EndTime = g.Event.EndTime,
                ProfileEvents = g.ProfileEvents.Select(pe => new ProfileEventDto
                {
                    ProfileHash = pe.ProfileId.ToString(),
                    EventRole = pe.Role,
                    Confirmed = pe.Confirmed,
                    Trusted = false
                }).ToList()
            });

        return await projectedResults.ToListAsync();
    }
    /*


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
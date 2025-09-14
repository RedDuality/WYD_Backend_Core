using MongoDB.Bson;
using Core.Model;
using Core.Model.Join;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Util;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.DTO.Model;
using Core.DTO.EventAPI;

namespace Core.Services.Model;


public class EventService(MongoDbService dbService, EventDetailsService eventDetailsService, ProfileEventService profileEventService, MediaService mediaService)
{
    private readonly CollectionName eventCollection = CollectionName.Events;

    private readonly CollectionName eventMediaCollection = CollectionName.EventMedia;

    private readonly BucketName eventBucket = BucketName.Events;

    public async Task<RetrieveEventResponseDto> CreateEventAsync(CreateEventRequestDto newEventDto, string profileId)
    {
        var newEvent = new Event
        {
            Title = newEventDto.Title!,
            StartTime = newEventDto.StartTime.ToUniversalTime(),
            EndTime = newEventDto.EndTime.ToUniversalTime(),
        };

        RetrieveEventResponseDto Event = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var createdEvent = await dbService.CreateOneAsync(eventCollection, newEvent, session);
            var eventDetails = await eventDetailsService.CreateAsync(createdEvent, newEventDto.Description, session);

            var newProfileEvent = new ProfileEvent
            (
                createdEvent, new ObjectId(profileId)
            )
            {
                Confirmed = true
            };
            var profileEvent = await profileEventService.CreateProfileEventAsync(newProfileEvent, session);

            return new RetrieveEventResponseDto(createdEvent, eventDetails, [profileEvent]);
        });

        return Event;
    }

    public async Task<RetrieveEventResponseDto> RetrieveEventById(string id)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, id);
        var eventDetails = await eventDetailsService.RetrieveByEventId(id);
        return new RetrieveEventResponseDto(ev, eventDetails);
    }

    public async Task ConfirmEventExists(string id)
    {
        await dbService.ConfirmExists<Event>(eventCollection, id);
    }


    /*
            public async Task<List<EventDto>> RetrieveEventsByProfileIdOld(List<string> profileHashes, DateTimeOffset startTime, DateTimeOffset? endTime)
            {
                var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

                var objectIds = profileHashes.Select(ph => new ObjectId(ph)).ToList();

                // Step 1: Define the filter using Builders
                var filterBuilder = Builders<ProfileEvent>.Filter;

                // Build the filter with logical AND conditions
                var filters = new List<FilterDefinition<ProfileEvent>>
                {
                    // Add the mandatory filters
                    filterBuilder.In(pe => pe.ProfileId, objectIds),
                    filterBuilder.Gte(pe => pe.EventEndTime, startTime.ToUniversalTime())
                };

                // Step 2: Conditionally add the end time filter
                if (endTime.HasValue)
                {
                    // Only apply the Less-than-or-equal filter if endTime has a value
                    filters.Add(filterBuilder.Lte(pe => pe.EventStartTime, endTime.Value.ToUniversalTime()));
                }

                // Combine all filters with a logical AND
                var filter = filterBuilder.And(filters);

                // Apply the filter to the aggregate pipeline
                var matchStage = aggregate.Match(filter)
                                          .Limit(40);

                // Step 3: Lookup the corresponding Event for each ProfileEvent
                // Join the two collecions in a ProfileEventWithCEvents object
                var lookupStage = matchStage.Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
                    dbService.GetCollection<Event>(eventCollection),
                    pe => pe.EventId,
                    e => e.Id,
                    pewce => pewce.Events);

                //flat out the results on a new projected object
                var projected = lookupStage
                    .Project(pe => new
                    {
                        Event = pe.Events[0],
                        pe.ProfileId,
                        pe.Role,
                        pe.Confirmed
                    });


                var intermediateResults = await projected.ToListAsync();

                // convert projected obj into eventDto
                var result = intermediateResults.Select(proj => new EventDto
                {
                    Hash = proj.Event.Id.ToString(),
                    Title = proj.Event.Title,
                    Description = proj.Event.Description,
                    StartTime = proj.Event.StartTime,
                    EndTime = proj.Event.EndTime,
                    ProfileEvents =
                    [
                        new ProfileEventDto {
                                ProfileHash = proj.ProfileId.ToString(),
                                EventRole = proj.Role,
                                Confirmed = proj.Confirmed,
                                Trusted = false
                            }
                    ]
                }).ToList();

                //var result = await projectStage.ToListAsync();


                return result;

            }

        */
    //RetrieveEventsRequestDto
    public async Task<List<RetrieveEventResponseDto>> RetrieveEventsByProfileId(List<string> profileHashes, DateTimeOffset startTime, DateTimeOffset? endTime)
    {
        var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

        var objectIds = profileHashes.Select(ph => new ObjectId(ph)).ToList();

        // Step 1: Define the filter using Builders
        var filterBuilder = Builders<ProfileEvent>.Filter;

        // Build the filter with logical AND conditions
        var filters = new List<FilterDefinition<ProfileEvent>>
        {
            // Add the mandatory filters
            filterBuilder.In(pe => pe.ProfileId, objectIds),
            filterBuilder.Gte(pe => pe.EventEndTime, startTime.ToUniversalTime())
        };

        // Step 2: Conditionally add the end time filter
        if (endTime.HasValue)
        {
            // Only apply the Less-than-or-equal filter if endTime has a value
            filters.Add(filterBuilder.Lte(pe => pe.EventStartTime, endTime.Value.ToUniversalTime()));
        }

        // Combine all filters with a logical AND
        var filter = filterBuilder.And(filters);

        // Apply the filter to the aggregate pipeline
        var matchStage = aggregate.Match(filter)
                                  .Limit(40);

        // Step 3: Lookup the corresponding Event for each ProfileEvent
        // Join the two collecions in a ProfileEventWithCEvents object
        var lookupStage = matchStage.Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
            dbService.GetCollection<Event>(eventCollection),
            pe => pe.EventId,
            e => e.Id,
            pewce => pewce.Events);

        //flat out the results on a new projected object
        var projected = lookupStage
            .Project(pe => new
            {
                Event = pe.Events[0],
                pe.ProfileId,
                pe.Role,
                pe.Confirmed
            });


        //var intermediateResults = await projected.ToListAsync();

        var grouped = projected.Group(
            pe => pe.Event.Id,
            group => new
            {
                ev = group.First().Event,
                ProfileEvents = group.Select(pe => new ProfileEventDto
                {
                    ProfileHash = pe.ProfileId.ToString(),
                    Role = pe.Role,
                    Confirmed = pe.Confirmed,
                    Trusted = false
                })
            }
        );

        var result = await grouped.ToListAsync();

        // Map the results over EventDto objects
        var finalResult = result.Select(g => new RetrieveEventResponseDto(g.ev, g.ProfileEvents.ToList())).ToList();

        return finalResult;

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

    public async Task<List<MediaUploadUrlDto>> GetMediaUploadUrlsAsync(Profile profile, MediaUploadDto dto)
    {
        await ConfirmEventExists(dto.ParentHash);

        return await mediaService.GetUploadUrlsAsync(profile, eventBucket, eventMediaCollection, dto);
    }

    public async Task<List<MediaReadUrlDto>> GetMediaReadUrlsAsync(Profile profile, List<string> eventHashes)
    {
        // TODO check profile permits over events
        return await mediaService.GetReadUrlsAsync(eventBucket, eventHashes);
    }
}
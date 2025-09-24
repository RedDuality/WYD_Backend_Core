using MongoDB.Bson;
using Core.Model;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Util;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.DTO.EventAPI;
using Core.Services.Util;
using Core.Services.Users;
using Core.Model.Profiles;
using Core.Model.Events;

namespace Core.Services.Events;


public class EventService(
    MongoDbService dbService,
    EventDetailsService eventDetailsService,
    ProfileEventService profileEventService,
    MediaService mediaService,
    BroadcastService broadcastService
)
{
    private readonly CollectionName eventCollection = CollectionName.Events;

    private readonly CollectionName eventMediaCollection = CollectionName.EventMedia;

    private readonly BucketName eventBucket = BucketName.Events;

    #region modify

    public async Task<RetrieveEventResponseDto> CreateEventAsync(CreateEventRequestDto newEventDto, string profileId)
    {
        var newEvent = new Event
        {
            Title = newEventDto.Title!,
            StartTime = newEventDto.StartTime.ToUniversalTime(),
            EndTime = newEventDto.EndTime.ToUniversalTime(),
        };

        RetrieveEventResponseDto EventDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            Event createdEvent = await dbService.CreateOneAsync(eventCollection, newEvent, session);
            EventDetails eventDetails = await eventDetailsService.CreateAsync(createdEvent, newEventDto.Description, session);

            ProfileEvent newProfileEvent = new(
                createdEvent,
                new ObjectId(profileId)
            )
            {
                Confirmed = true
            };

            ProfileEvent profileEvent = await profileEventService.CreateProfileEventAsync(newProfileEvent, session);

            return new RetrieveEventResponseDto(createdEvent, eventDetails, [profileEvent]);
        });

        _ = broadcastService.BroadcastEventUpdate(EventDto.Hash, UpdateType.CreateEvent, "A new event was just created", "yeee, new events");
        return EventDto;
    }

    public async Task<RetrieveEventResponseDto> UpdateEventAsync(UpdateEventRequestDto updateDto)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, updateDto.EventId);

        var updates = new List<UpdateDefinition<Event>>();

        // Add updates to the list based on non-null values
        if (updateDto.Title != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.Title, updateDto.Title));
        }

        if (updateDto.StartTime != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.StartTime, updateDto.StartTime));
        }

        if (updateDto.EndTime != null)
        {
            updates.Add(Builders<Event>.Update.Set(e => e.EndTime, updateDto.EndTime));
        }

        // Check if there are any updates to perform
        if (updates.Count != 0)
        {
            var combinedUpdate = Builders<Event>.Update.Combine(updates);

            ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, combinedUpdate);

            _ = broadcastService.BroadcastEventUpdate(ev.Id.ToString(), UpdateType.UpdateEssentialsEvent, "Un evento è stato aggiornato", "Better not be the medic visit");
        }

        EventDetails? details = null;
        if (updateDto.Description != null)
        {
            details = await eventDetailsService.Update(ev.Id, updateDto.Description);
        }

        return new RetrieveEventResponseDto(ev, details: details);
    }

    public async Task Confirm(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            await profileEventService.Confirm(profileId, eventId, session);

            var increaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, 1);
            var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), increaseUpdate, session);

            _ = broadcastService.BroadcastEventUpdate(eventId, UpdateType.ConfirmEvent, profileId: profileId);
            return null;
        });
    }

    public async Task Decline(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            await profileEventService.Decline(profileId, eventId, session);

            var decreaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, -1);
            var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), decreaseUpdate, session);

            _ = broadcastService.BroadcastEventUpdate(eventId, UpdateType.DeclineEvent, profileId: profileId);
            return null;
        });
    }

    #endregion

    #region retrieve

    public async Task ConfirmEventExists(string id)
    {
        await dbService.ConfirmExists<Event>(eventCollection, id);
    }

    public async Task<RetrieveEventResponseDto> RetrieveEventById(string eventId, string profileId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var pe = await profileEventService.FindByProfileAndEventId(profileId, eventId);
        return new RetrieveEventResponseDto(ev, profileEvents: [pe]);
    }

    public async Task<RetrieveEventResponseDto> RetrieveEventWithDetailsById(string eventId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var eventDetails = await eventDetailsService.RetrieveByEventId(eventId);
        return new RetrieveEventResponseDto(ev, details: eventDetails);
    }

    public async Task<List<RetrieveEventResponseDto>> RetrieveEventsByProfileIds(RetrieveMultipleEventsRequestDto requestDto)
    {
        var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

        var objectIds = requestDto.ProfileHashes.Select(ph => new ObjectId(ph)).ToList();

        // Step 1: Define the filter using Builders
        var filterBuilder = Builders<ProfileEvent>.Filter;

        // Build the filter with logical AND conditions
        var filters = new List<FilterDefinition<ProfileEvent>>
        {
            // Add the mandatory filters
            filterBuilder.In(pe => pe.ProfileId, objectIds),
            filterBuilder.Gte(pe => pe.EventEndTime, requestDto.StartTime.ToUniversalTime())
        };

        // Step 2: Conditionally add the end time filter
        if (requestDto.EndTime.HasValue)
        {
            // Only apply the Less-than-or-equal filter if endTime has a value
            filters.Add(filterBuilder.Lte(pe => pe.EventStartTime, requestDto.EndTime.Value.ToUniversalTime()));
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
                }).ToList()
            }
        );

        var result = await grouped.ToListAsync();

        // Map the results over EventDto objects
        var finalResult = result.Select(g => new RetrieveEventResponseDto(g.ev, profileEventDtos: g.ProfileEvents)).ToList();

        return finalResult;

    }

    #endregion

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

    #region media

    public async Task<List<MediaUploadResponseDto>> GetMediaUploadUrlsAsync(Profile profile, MediaUploadRequestDto dto)
    {
        await ConfirmEventExists(dto.ParentHash);

        var dtos = await mediaService.GetUploadUrlsAsync(profile, eventBucket, eventMediaCollection, dto);
        // TODO move this to after the images have been checked
        var okImages = dtos.Where((dto) => dto.Error == null).Count();
        await eventDetailsService.AddImages(okImages, dto.ParentHash);
        return dtos;
    }

    public async Task<List<MediaReadResponseDto>> GetMediaReadUrlsAsync(Profile profile, MediaReadRequestDto mediaReadRequestDto)
    {
        // TODO check profile permits over events
        return await mediaService.GetReadUrlsAsync(eventBucket, mediaReadRequestDto);
    }

    #endregion

}
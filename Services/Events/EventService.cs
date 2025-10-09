using MongoDB.Bson;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Util;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.DTO.EventAPI;
using Core.Services.Users;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.DTO.CommunityAPI;
using Core.Services.Communities;
using Core.Model.Notifications;
using Core.Components.MessageQueue;

namespace Core.Services.Events;


public class EventService(
    MongoDbService dbService,
    EventDetailsService eventDetailsService,
    ProfileEventService profileEventService,
    EventProfileService eventProfileService,
    GroupService groupService,
    MediaService mediaService,
    MessageQueueService messageService
)
{
    private readonly CollectionName eventCollection = CollectionName.Events;

    private readonly CollectionName eventMediaCollection = CollectionName.EventMedia;

    private readonly BucketName eventBucket = BucketName.Events;

    #region modify

    public async Task<RetrieveEventResponseDto> CreateEventAsync(CreateEventRequestDto newEventDto, string profileId)
    {
        var ev = new Event
        {
            Title = newEventDto.Title!,
            StartTime = newEventDto.StartTime.ToUniversalTime(),
            EndTime = newEventDto.EndTime.ToUniversalTime(),
        };

        RetrieveEventResponseDto EventDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            await dbService.CreateOneAsync(eventCollection, ev, session);
            EventDetails eventDetails = await eventDetailsService.CreateAsync(ev, newEventDto.Description, session);
            ProfileEvent profileEvent = await profileEventService.CreateProfileEventAsync(ev, new ObjectId(profileId), session);

            return new RetrieveEventResponseDto(ev, eventDetails, [profileEvent]);
        });

        var notification = new Notification(
            EventDto.Hash,
            NotificationType.CreateEvent
        )
        {
            Title = "A new event was just created",
            Body = "yeee, new events",
        };
        await messageService.SendNotificationAsync(notification);
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

        EventDetails? details = null;

        var upatedEvent = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            // Check if there are any updates to perform
            if (updates.Count != 0)
            {
                var combinedUpdate = Builders<Event>.Update.Combine(updates);

                ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, combinedUpdate, session);

                var notification = new Notification(
                    ev.Id.ToString(),
                    NotificationType.UpdateEssentialsEvent
                )
                {
                    Title = "Un evento è stato aggiornato",
                    Body = "Better not be the medic visit",
                };
                await messageService.SendNotificationAsync(notification);

            }


            if (updateDto.Description != null)
            {
                details = await eventDetailsService.Update(ev.Id, updateDto.Description, session);
            }
            return ev;
        });


        return new RetrieveEventResponseDto(upatedEvent, details: details);
    }

    public async Task<RetrieveEventResponseDto> ShareAsync(Profile profile, string eventId, List<ShareEventRequestDto> groupIds)
    {

        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);

        var profiles = await groupService.GetProfilesByGroupIds(groupIds, profile);

        var alreadyExistingProfiles = await eventProfileService.FindAlreadyExisting(ev, profiles);
        profiles.ExceptWith(alreadyExistingProfiles);

        var updateDefinition = Builders<Event>.Update.Inc(e => e.TotalProfilesMinusOne, profiles.Count);

        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
                {
                    ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, updateDefinition, session);
                    await profileEventService.CreateMultipleProfileEventAsync(
                        ev,
                        profiles,
                        session);

                    return null;
                });
        return new RetrieveEventResponseDto(ev);

    }

    public async Task Confirm(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            await profileEventService.Confirm(profileId, eventId, session);

            var increaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, 1);
            var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), increaseUpdate, session);

            var notification = new Notification(
                eventId,
                NotificationType.ConfirmEvent
            )
            {
                ProfileId = profileId
            };
            await messageService.SendNotificationAsync(notification);

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

            var notification = new Notification(
                eventId,
                NotificationType.DeclineEvent
            )
            {
                ProfileId = profileId
            };
            await messageService.SendNotificationAsync(notification);

            return null;
        });
    }

    #endregion

    #region retrieve

    public async Task ConfirmEventExists(string id)
    {
        await dbService.ConfirmExists<Event>(eventCollection, id);
    }

    // for RT updates(creation/share of an event)
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
using MongoDB.Bson;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Util;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.DTO.CommunityAPI;
using Core.Services.Communities;
using Core.Model.Notifications;
using Core.Components.MessageQueue;
using Core.Model.QueueMessages;
using Core.Services.Profiles;

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

    #region create

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
            ev.Id,
            NotificationType.UpdateEssentialsEvent,
            ev.UpdatedAt
        )
        {
            //ActorId = profileId,
        };
        await messageService.SendNotificationAsync(notification);

        return EventDto;
    }

    // user open a link for an event it should not have, so, if null, we "create" the related profileEvent 
    public async Task<RetrieveEventResponseDto> CreateAndRetrieveSharedEvent(string eventId, string profileId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var eventDetails = await eventDetailsService.RetrieveByEventId(eventId);

        var pe = await profileEventService.FindByProfileAndEventId(profileId, eventId);
        pe ??= await dbService.ExecuteInTransactionAsync(async (session) =>
            {
                var createdPe = await profileEventService.CreateProfileEventAsync(ev, new ObjectId(profileId), session, false);

                var updateDefinition = Builders<Event>.Update.Inc(e => e.TotalProfilesMinusOne, 1);
                ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, updateDefinition, session);

                var propagationMessage = new QueueMessage<UpdateEventPayload>(MessageType.eventUpdate, new(ev, Model.QueueMessages.EventUpdateType.update));
                await messageService.SendPropagationMessageAsync(propagationMessage);

                return createdPe;
            });

        return new RetrieveEventResponseDto(ev, details: eventDetails, profileEvents: [pe]);
    }

    public async Task<RetrieveEventResponseDto> ShareAsync(Profile profile, string eventId, List<ShareEventRequestDto> groupIds)
    {

        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var profileIds = await FindAffectedByShare(groupIds, profile, ev);
        var profileNumber = profileIds.Count;
        if (profileNumber > 0)
        {
            var updateDefinition = Builders<Event>.Update.Inc(e => e.TotalProfilesMinusOne, profileNumber);

            ev = await dbService.ExecuteInTransactionAsync(async (session) =>
            {
                await profileEventService.CreateMultipleProfileEventAsync(ev, profileIds, session);

                ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, updateDefinition, session);

                var propagationMessage = new QueueMessage<UpdateEventPayload>(MessageType.eventUpdate, new(ev, Model.QueueMessages.EventUpdateType.share));
                await messageService.SendPropagationMessageAsync(propagationMessage);

                return ev;
            });
        }
        return new RetrieveEventResponseDto(ev);
    }

    private async Task<HashSet<ObjectId>> FindAffectedByShare(List<ShareEventRequestDto> dtos, Profile profile, Event ev)
    {
        var profiles = await groupService.GetProfilesByGroupIds(dtos, profile);

        // remove profiles which event has alreasy been shared
        var alreadyExistingProfiles = await eventProfileService.FindAlreadyExisting(ev, profiles);
        profiles.ExceptWith(alreadyExistingProfiles);

        return profiles.ToHashSet();
    }

    #endregion

    #region modify
    public async Task<RetrieveEventResponseDto> UpdateEventAsync(UpdateEventRequestDto updateDto)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, updateDto.EventId);

        var updates = GetUpdates(updateDto);

        EventDetails? details = null;

        var upatedEvent = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            if (updateDto.Description != null)
            {
                details = await eventDetailsService.Update(ev.Id, updateDto.Description, session);
            }

            // Check if there are any updates to perform
            if (updates.Count != 0)
            {
                var combinedUpdate = Builders<Event>.Update.Combine(updates);

                ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, combinedUpdate, session);

                var propagationMessage = new QueueMessage<UpdateEventPayload>(MessageType.eventUpdate, new(ev, Model.QueueMessages.EventUpdateType.update));
                await messageService.SendPropagationMessageAsync(propagationMessage);
            }

            return ev;
        });


        return new RetrieveEventResponseDto(upatedEvent, details: details);
    }

    private static List<UpdateDefinition<Event>> GetUpdates(UpdateEventRequestDto updateDto)
    {
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

        return updates;
    }

    /*
        public async Task PropagateUpdateEffects(Event ev, UpdateType type, string? actorId = null)
        {
            var profileIds = await eventProfileService.GetProfileIdsAsync(ev.Id);
            if (profileIds.Count > 0)
            {
                await profileEventService.PropagateEventUpdatesAsync(ev, profileIds);

                var notification = GetUpdateNotification(type, ev, actorId);
                await messageService.SendNotificationAsync(notification);
            }
        }

        private static Notification GetUpdateNotification(UpdateType type, Event ev, string? actorId = null)
        {
            return type switch
            {
                UpdateType.share => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent) { UpdatedAt = ev.UpdatedAt},
                UpdateType.update => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent) { UpdatedAt = ev.UpdatedAt},
                UpdateType.confirm => new Notification(ev.Id, NotificationType.ConfirmEvent) { ActorId = actorId },
                UpdateType.decline => new Notification(ev.Id, NotificationType.DeclineEvent) { ActorId = actorId },
                _ => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent),
            };
        }
    */

    public async Task Confirm(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            var changed = await profileEventService.Confirm(profileId, eventId, session);
            if (changed)
            {
                var increaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, 1);
                var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), increaseUpdate, session);

                var propagationMessage = new QueueMessage<UpdateEventPayload>(MessageType.eventUpdate, new(ev, Model.QueueMessages.EventUpdateType.confirm, profileId));
                await messageService.SendPropagationMessageAsync(propagationMessage);
            }
            return null;
        });
    }

    public async Task Decline(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            var changed = await profileEventService.Decline(profileId, eventId, session);
            if (changed)
            {
                var decreaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, -1);
                var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), decreaseUpdate, session);

                var propagationMessage = new QueueMessage<UpdateEventPayload>(MessageType.eventUpdate, new(ev, Model.QueueMessages.EventUpdateType.decline, profileId));
                await messageService.SendPropagationMessageAsync(propagationMessage);
            }

            return null;
        });
    }

    #endregion

    #region retrieve

    public async Task CheckEventExists(string id)
    {
        await dbService.ConfirmExists<Event>(eventCollection, id);
    }

    // for RT updates(creation/share of an event)
    public async Task<RetrieveEventResponseDto> RetrieveEventById(string eventId, string profileId)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);
        var pe = await profileEventService.FindByProfileAndEventId(profileId, eventId);
        return new RetrieveEventResponseDto(ev, profileEvents: [pe!]);
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

    #region media

    public async Task<List<MediaUploadResponseDto>> GetMediaUploadUrlsAsync(Profile profile, MediaUploadRequestDto dto)
    {
        await CheckEventExists(dto.ParentHash);

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
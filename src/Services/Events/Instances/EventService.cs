using MongoDB.Bson;
using Core.Components.Database;
using MongoDB.Driver;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.DTO.EventAPI;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.DTO.CommunityAPI;
using Core.Services.Communities;
using Core.Components.MessageQueue;
using Core.Model.QueueMessages;
using Core.Services.Profiles;
using Core.Model.Util.EventsQuery;

namespace Core.Services.Events.Instances;

public class EventService(
    MongoDbService dbService,
    EventDetailsService eventDetailsService,
    ProfileEventService profileEventService,
    EventProfileService eventProfileService,
    GroupService groupService,
    MediaService mediaService,
    IMessageQueueService messageService
)
{
    private readonly CollectionName eventCollection = CollectionName.Events;

    private readonly CollectionName eventMediaCollection = CollectionName.EventMedia;

    private readonly BucketName eventBucket = BucketName.Events;

    #region create

    public async Task<RetrieveEventResponseDto> CreateEventAsync(CreateEventRequestDto newEventDto, Profile creatorProfile)
    {
        var sharedProfileIds = await GetSharedProfileIds(newEventDto, creatorProfile);
        var ev = new Event(newEventDto.Title, newEventDto.StartTime, newEventDto.EndTime) { TotalProfilesMinusOne = sharedProfileIds.Count };

        RetrieveEventResponseDto EventDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var (newEvent, details, profileEvent) = await CreateEvent(ev, creatorProfile, sharedProfileIds, newEventDto.Description, session);
            return new RetrieveEventResponseDto(newEvent, details, [profileEvent]);
        });
        return EventDto;
    }

    private async Task<HashSet<ObjectId>> GetSharedProfileIds(CreateEventRequestDto newEventDto, Profile profile)
    {
        HashSet<ObjectId> sharedProfileIds = [];
        if (newEventDto.ShareDto != null && newEventDto.ShareDto.SharedGroups.Count > 0)
        {
            sharedProfileIds = await groupService.GetProfilesByGroupIds(newEventDto.ShareDto.SharedGroups, profile);
        }
        return sharedProfileIds;
    }

    public async Task<(Event, EventDetails, ProfileEvent)> CreateEvent(
        Event ev,
        Profile creatorProfile,
        HashSet<ObjectId> otherProfilesIds,
        string? description,
        IClientSessionHandle session)
    {
        await dbService.CreateOneAsync(eventCollection, ev, session);
        EventDetails eventDetails = await eventDetailsService.CreateAsync(ev, description, session);
        ProfileEvent profileEvent = await profileEventService.CreateProfileEventAsync(ev, creatorProfile.Id, session, role: EventRole.Owner);

        if (otherProfilesIds.Count > 0)
            ev = await ShareEvent(ev, otherProfilesIds, false, session);

        await SendCreatePropagationMessage(ev, creatorProfile);

        return (ev, eventDetails, profileEvent);
    }



    private async Task SendCreatePropagationMessage(Event ev, Profile creatorProfile)
    {
        var propagationMessage = new QueueMessage<EventPayload>(
                MessageType.eventUpdate,
                new(ev, EventUpdateType.create, actorId: creatorProfile.Id.ToString())
            );
        await messageService.SendPropagationMessageAsync(propagationMessage);
    }

    public async Task<RetrieveEventResponseDto> ShareEventAsync(Profile profile, string eventId, ShareEventRequestDto shareDto)
    {
        var ev = await dbService.RetrieveByIdAsync<Event>(eventCollection, eventId);

        var profileIds = await FindAffectedByShare(shareDto, profile, ev);

        if (profileIds.Count > 0)
            ev = await dbService.ExecuteInTransactionAsync(async (session) =>
                {
                    ev = await ShareEvent(ev, profileIds, true, session);

                    await SendSharePropagationMessage(ev);

                    return ev;
                });

        return new RetrieveEventResponseDto(ev);
    }

    private async Task<HashSet<ObjectId>> FindAffectedByShare(ShareEventRequestDto shareDto, Profile currentProfile, Event ev)
    {
        var profileIds = await groupService.GetProfilesByGroupIds(shareDto.SharedGroups, currentProfile);

        // remove profiles which event has already been shared
        var alreadyExistingProfiles = await eventProfileService.FindAlreadyExisting(ev, profileIds);
        profileIds.ExceptWith(alreadyExistingProfiles);

        return profileIds;
    }

    private async Task SendSharePropagationMessage(Event ev)
    {
        var propagationMessage = new QueueMessage<EventPayload>(
                        MessageType.eventUpdate,
                        new(ev, EventUpdateType.share)
                    );
        await messageService.SendPropagationMessageAsync(propagationMessage);
    }

    private async Task<Event> ShareEvent(Event ev, HashSet<ObjectId> profileIds, bool alreadyExisted, IClientSessionHandle session)
    {
        await profileEventService.CreateMultipleProfileEventAsync(ev, profileIds, session);

        if (alreadyExisted)
        {
            var updateDefinition = Builders<Event>.Update.Inc(e => e.TotalProfilesMinusOne, profileIds.Count);
            ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, ev.Id, updateDefinition, session);
        }

        return ev;
    }

    // user open a link for an event it should not have, and, IF NOT ALREADY EXISTING, we create the related profileEvent 
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

                await SendSharePropagationMessage(ev);

                return createdPe;
            });

        return new RetrieveEventResponseDto(ev, details: eventDetails, profileEvents: [pe]);
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

                var propagationMessage = new QueueMessage<EventPayload>(MessageType.eventUpdate, new(ev, EventUpdateType.update));
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

    public async Task Confirm(string eventId, string profileId)
    {
        await dbService.ExecuteInTransactionAsync<object?>(async (session) =>
        {
            var changed = await profileEventService.Confirm(profileId, eventId, session);
            if (changed)
            {
                var increaseUpdate = Builders<Event>.Update.Inc(ev => ev.TotalConfirmedMinusOne, 1);
                var ev = await dbService.FindOneByIdAndUpdateAsync(eventCollection, new ObjectId(eventId), increaseUpdate, session);

                var propagationMessage = new QueueMessage<EventPayload>(MessageType.eventUpdate, new(ev, EventUpdateType.confirm, profileId));
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

                var propagationMessage = new QueueMessage<EventPayload>(MessageType.eventUpdate, new(ev, EventUpdateType.decline, profileId));
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

    public async Task<List<RetrieveEventResponseDto>> RetrieveEventsByProfileIds(List<ObjectId> profileIds, RetrieveMultipleEventsRequestDto requestDto)
    {
        // create pipeline, to have the db handle everything in one operation
        var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.In(pe => pe.ProfileId, profileIds),
            Builders<ProfileEvent>.Filter.Gte(pe => pe.EventEndTime, requestDto.StartTime.ToUniversalTime()),
            Builders<ProfileEvent>.Filter.Lte(pe => pe.EventStartTime, requestDto.EndTime.ToUniversalTime())
        );

        // Apply the filter to the aggregate pipeline
        var profileEvents = aggregate.Match(filter)
                                  .Limit(100);

        // Step 2: Lookup the Event collection, and
        // join the two collections in a ProfileEventWithCorrEvents(pewce) object
        var lookupStage = profileEvents.Lookup<ProfileEvent, Event, ProfileEventWithCorrespondingEvents>(
            dbService.GetCollection<Event>(eventCollection),
            pe => pe.EventId,
            e => e.Id,
            pewce => pewce.Events);

        // flat out the results on a new projected object
        var projections = lookupStage
            .Project(pewce => new
            {
                Event = pewce.Events[0],
                pewce.ProfileId,
                pewce.Role,
                pewce.Confirmed
            });


        //var intermediateResults = await projected.ToListAsync();

        // now that we have all the events (one for each profile), 
        // we group them by eventId, listing the profiles into ProfileEventDto
        var grouped = projections.Group(
            projected => projected.Event.Id,
            groupedEvents => new
            {
                ev = groupedEvents.First().Event,
                ProfileEvents = groupedEvents.Select(projection => new ProfileEventDto
                {
                    ProfileId = projection.ProfileId.ToString(),
                    Role = projection.Role,
                    Confirmed = projection.Confirmed,
                    Trusted = false
                }).ToList()
            }
        );

        var result = await grouped.ToListAsync();

        // Map the results over EventDto objects
        var finalResult = result.Select(g => new RetrieveEventResponseDto(g.ev, profileEventDtos: g.ProfileEvents)).ToList();

        return finalResult;

    }

    public async Task<List<RetrieveEventResponseDto>> RetrieveUpdatesByProfileIds(RetrieveUpdatedEventsRequestDto requestDto)
    {
        var aggregate = dbService.GetAggregate<ProfileEvent>(CollectionName.ProfileEvents);

        var objectIds = requestDto.ProfileIds.Select(ph => new ObjectId(ph)).ToList();

        var filterBuilder = Builders<ProfileEvent>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.In(pe => pe.ProfileId, objectIds),
            filterBuilder.Gte(pe => pe.UpdatedAt, requestDto.UpdatedAfterTime.ToUniversalTime())
         );

        // Apply the filter to the aggregate pipeline
        var matchStage = aggregate.Match(filter);

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
                    ProfileId = pe.ProfileId.ToString(),
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

    public async Task<HashSet<ProfileEventDto>> GetProfileEventsAsync(string eventId)
    {
        var eps = await eventProfileService.FindAllByEventId(new ObjectId(eventId));

        // Build the (profileId, eventId) pairs
        var profileEventPairs = eps
            .Select(ep => (ep.ProfileId.ToString(), eventId))
            .ToList();

        return await profileEventService.FindMultipleByProfileAndEventIds(profileEventPairs);
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
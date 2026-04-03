using Core.Components.Database;
using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Model.Masks;
using Core.Model.Profiles;

namespace Core.External.ImportPlatform;

public class ImportService(MongoDbService dbService)
{
    public async Task SaveMultipleEvents(List<ImportEventDto> events, List<ImportRecurrentEventDto> recurrentEvents, Profile profile)
    {
        await ImportMultipleEventsAsync(events, profile);
        await ImportMultipleRecurrentEventsAsync(recurrentEvents, profile);
    }

    private async Task ImportMultipleEventsAsync(
    List<ImportEventDto> newEventDtos,
    Profile creatorProfile)
    {
        var events = newEventDtos
            .Select(dto => new Event(dto.Title, dto.StartTime, dto.EndTime)
            {
                IsAllDay = dto.IsAllDay,
                ImportedAccountUid = dto.ImportedAccountUid,
                ExternalEventId = dto.ExternalEventId,
                ExternalMasterEventId = dto.ExternalMasterEventId,
            })
            .ToList();

        var eventDetails = events
            .Zip(newEventDtos, (ev, dto) => new EventDetails(ev) { Description = dto.Description })
            .ToList();

        var profileEvents = events
            .Select(ev => new ProfileEvent(ev, creatorProfile.Id) { Confirmed = true, Role = EventRole.Owner })
            .ToList();

        var eventProfiles = profileEvents
            .Select(pe => new EventProfile(pe))
            .ToList();

        var masks = events
            .Select(ev => new Mask(creatorProfile.Id, ev))
            .ToList();

        await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            await dbService.CreateManyAsync(CollectionName.Events, events, session);
            await dbService.CreateManyAsync(CollectionName.EventDetails, eventDetails, session);
            await dbService.CreateManyAsync(CollectionName.ProfileEvents, profileEvents, session);
            await dbService.CreateManyAsync(CollectionName.EventProfiles, eventProfiles, session);
            await dbService.CreateManyAsync(CollectionName.Masks, masks, session);
            return true;
        });
    }

    private async Task ImportMultipleRecurrentEventsAsync(
    List<ImportRecurrentEventDto> newRecurrentEventDtos,
    Profile creatorProfile)
    {
        var events = newRecurrentEventDtos
            .Select(dto => new RecurrentEvent(
                dto.Title, 
                dto.StartTime, 
                dto.EndTime, 
                dto.TimeZone, 
                dto.RecurrenceRule)
                {
                    IsAllDay = dto.IsAllDay,
                })
            .ToList();

        var eventDetails = events
            .Zip(newRecurrentEventDtos, (ev, dto) => new EventDetails(ev) { Description = dto.Description })
            .ToList();

        var profileEvents = events
            .Select(ev => new ProfileRecurrentEvent(ev, creatorProfile.Id))
            .ToList();

        var eventProfiles = profileEvents
            .Select(pe => new RecurrentEventProfile(pe))
            .ToList();

        var masks = events
            .Select(ev => new Mask(creatorProfile.Id, ev))
            .ToList();

        await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            await dbService.CreateManyAsync(CollectionName.RecurrentEvents, events, session);
            await dbService.CreateManyAsync(CollectionName.EventDetails, eventDetails, session);
            await dbService.CreateManyAsync(CollectionName.ProfileEvents, profileEvents, session);
            await dbService.CreateManyAsync(CollectionName.EventProfiles, eventProfiles, session);
            await dbService.CreateManyAsync(CollectionName.Masks, masks, session);
            return true;
        });
    }
}
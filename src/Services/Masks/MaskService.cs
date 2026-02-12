using Core.Components.Database;
using Core.Components.MessageQueue;
using Core.DTO.MaskAPI;
using Core.Model.Masks;
using Core.Model.Notifications;
using Core.Services.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Masks;

public class MaskService(
    MongoDbService dbService,
    UserService userService,
    IMessageQueueService messageService,
    ImportedProfilesService importedProfilesService
)
{
    private readonly CollectionName maskCollection = CollectionName.Masks;

    #region modify

    public async Task<RetrieveMaskResponseDto> CreateMaskAsync(string profileId, CreateMaskRequestDto createDto)
    {
        Mask mask = new(new ObjectId(profileId), createDto.StartTime, createDto.EndTime, createDto.Title);
        await dbService.CreateOneAsync(maskCollection, mask, null);

        var notification = new Notification(mask.Id, NotificationType.UpdateMask, updatedAt: mask.UpdatedAt) { ActorId = profileId };
        await messageService.SendNotificationAsync(notification);

        return new RetrieveMaskResponseDto(mask);
    }

    public async Task<RetrieveMaskResponseDto> UpdateMaskAsync(string profileId, UpdateMaskRequestDto updateDto)
    {
        var updates = GetUpdates(updateDto);

        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<Mask>.Filter.Eq(m => m.Id, new ObjectId(updateDto.MaskId)),
            Builders<Mask>.Filter.Eq(m => m.EventId, null) // Cannot update if EventId is set
        );

        Mask updatedMask;
        try
        {
            updatedMask = await dbService.FindOneAndUpdateAsync(
                maskCollection,
                filter,
                updates
            );

        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException($"Update failed. Mask '{updateDto.MaskId}' either does not exist or is linked to an event and cannot be modified directly.");
        }

        var notification = new Notification(updatedMask.Id, NotificationType.UpdateMask, updatedAt: updatedMask.UpdatedAt) { ActorId = profileId };
        await messageService.SendNotificationAsync(notification);

        return new RetrieveMaskResponseDto(updatedMask);
    }

    private static UpdateDefinition<Mask> GetUpdates(UpdateMaskRequestDto updateDto)
    {
        var updates = new List<UpdateDefinition<Mask>>
        {
            Builders<Mask>.Update.Set(e => e.Title, updateDto.Title)
        };

        if (updateDto.StartTime != null)
            updates.Add(Builders<Mask>.Update.Set(e => e.StartTime, updateDto.StartTime));

        if (updateDto.EndTime != null)
            updates.Add(Builders<Mask>.Update.Set(e => e.EndTime, updateDto.EndTime));

        return Builders<Mask>.Update.Combine(updates); ;
    }

    public async Task DeleteMaskAsync(string profileId, string maskId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<Mask>.Filter.Eq(m => m.Id, new ObjectId(maskId))
        );

        await dbService.DeleteOneAsync(maskCollection, filter);

        var notification = new Notification(new ObjectId(maskId), NotificationType.DeleteMask) { ActorId = profileId };
        await messageService.SendNotificationAsync(notification);
    }

    #endregion

    #region retrieve

    public async Task<RetrieveMaskResponseDto> RetrieveSingleMask(string profileId, string maskId)
    {
        var filter = Builders<Mask>.Filter.And(
            Builders<Mask>.Filter.Eq(m => m.ProfileId, new ObjectId(profileId)),
            Builders<Mask>.Filter.Eq(m => m.Id, new ObjectId(maskId))
        );

        var mask = await dbService.RetrieveOrNullAsync(maskCollection, filter) ?? throw new InvalidOperationException("Mask not found");

        return new RetrieveMaskResponseDto(mask);
    }


    public async Task<List<RetrieveMaskResponseDto>> RetrieveUserMasks(string userId, RetrieveUserMaskRequestDto retrieveDto)
    {
        var profileObjectIds = await userService.GetProfileIds(userId);

        var filterBuilder = Builders<Mask>.Filter;

        var filters = new List<FilterDefinition<Mask>>
        {
            // Add the mandatory filters
            filterBuilder.In(m => m.ProfileId, profileObjectIds),
            filterBuilder.Gte(m => m.EndTime, retrieveDto.StartTime.ToUniversalTime()),
        };

        if (retrieveDto.EndTime.HasValue)
        {
            // Only apply the Less-than-or-equal filter if endTime has a value
            filters.Add(filterBuilder.Lte(pe => pe.StartTime, retrieveDto.EndTime.Value.ToUniversalTime()));
        }

        var filter = filterBuilder.And(filters);


        var masks = await dbService.RetrieveMultipleAsync(maskCollection, filter);

        return [.. masks.Select(m => new RetrieveMaskResponseDto(m))];
    }

    public async Task<List<RetrieveMaskResponseDto>> RetrieveUpdated(string userId, RetrieveUserMaskRequestDto retrieveDto)
    {
        var profileObjectIds = await userService.GetProfileIds(userId);

        var filterBuilder = Builders<Mask>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.In(m => m.ProfileId, profileObjectIds),
            filterBuilder.Gte(m => m.UpdatedAt, retrieveDto.StartTime.ToUniversalTime())
        );

        var masks = await dbService.RetrieveMultipleAsync(maskCollection, filter);

        return [.. masks.Select(m => new RetrieveMaskResponseDto(m))];
    }


    public async Task<List<RetrieveViewMaskResponseDto>> RetrieveProfileMasks(RetrieveProfileMaskRequestDto retrieveDto)
    {

        var profileId = new ObjectId(retrieveDto.ProfileId);
        var relatedProfiles = await importedProfilesService.GetImportedProfiles(profileId);
        relatedProfiles.Add(profileId);

        var filterBuilder = Builders<Mask>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.In(m => m.ProfileId, relatedProfiles),
            filterBuilder.Gte(m => m.EndTime, retrieveDto.StartTime.ToUniversalTime()),
            filterBuilder.Lte(m => m.StartTime, retrieveDto.EndTime.ToUniversalTime())
        );

        var sort = Builders<Mask>.Sort.Ascending(m => m.StartTime);
        var masks = await dbService.FindForPaginationAsync(maskCollection, filter, sort, null, null);

        var unifiedMasks = UnifyMasks(masks);
        return [.. unifiedMasks.Select(m => new RetrieveViewMaskResponseDto(m))];
    }

    private static List<Mask> UnifyMasks(List<Mask> masks)
    {

        if (masks == null || masks.Count <= 1)
            return masks ?? [];

        return masks
            .Aggregate(new List<Mask>(), (unified, next) =>
            {
                var last = unified.LastOrDefault();

                // If list is empty or there is no overlap, add the next mask
                if (last == null || next.StartTime > last.EndTime)
                {
                    unified.Add(next);
                }
                else
                {
                    // Partial or total overlap: update the EndTime of the existing mask
                    if (next.EndTime > last.EndTime)
                    {
                        last.EndTime = next.EndTime;
                    }
                }

                return unified;
            });
    }

    #endregion

}
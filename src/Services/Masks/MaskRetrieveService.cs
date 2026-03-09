using Core.Components.Database;
using Core.DTO.MaskAPI;
using Core.Model.Masks;
using Core.Services.Users;
using Core.Services.Util;
using Ical.Net.DataTypes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Masks;

public class MaskRetrieveService(
    MongoDbService dbService,
    UserService userService,
    ImportedProfilesService importedProfilesService
)
{
    private readonly CollectionName maskCollection = CollectionName.Masks;
    private readonly CollectionName recurrentMaskCollection = CollectionName.RecurrentMasks;

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

        var masksTask = RetrieveInstanceMasks(retrieveDto, relatedProfiles);
        var recurrentMasksTask = RetrieveRecurrentMasks(retrieveDto, relatedProfiles);

        var results = await Task.WhenAll(masksTask, recurrentMasksTask);

        var instancesMasks = SubstituteWithInstances(results[1], results[0]);

        var unifiedMasks = UnifyMasks(instancesMasks);
        return [.. unifiedMasks.Select(m => new RetrieveViewMaskResponseDto(m))];
    }

    private async Task<List<Mask>> RetrieveInstanceMasks(RetrieveProfileMaskRequestDto retrieveDto, HashSet<ObjectId> profiles)
    {
        var filterBuilder = Builders<Mask>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.In(m => m.ProfileId, profiles),
            filterBuilder.Gte(m => m.EndTime, retrieveDto.StartTime.ToUniversalTime()),
            filterBuilder.Lte(m => m.StartTime, retrieveDto.EndTime.ToUniversalTime())
        );


        var masks = await dbService.RetrieveMultipleAsync(maskCollection, filter);
        return masks;
    }

    private async Task<List<Mask>> RetrieveRecurrentMasks(RetrieveProfileMaskRequestDto retrieveDto, HashSet<ObjectId> profiles)
    {
        var filterBuilder = Builders<RecurrentMask>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.In(m => m.ProfileId, profiles),
            filterBuilder.Gte(m => m.RecurrenceEnd, retrieveDto.StartTime.ToUniversalTime()),
            filterBuilder.Lte(m => m.StartTime, retrieveDto.EndTime.ToUniversalTime())
        );

        var recurrentMasks = await dbService.RetrieveMultipleAsync(recurrentMaskCollection, filter);
        var masks = recurrentMasks.SelectMany(
            m => ExpandRecurrentMask(
                m,
                new ObjectId(retrieveDto.ProfileId),
                retrieveDto.StartTime,
                retrieveDto.EndTime)
            ).ToList();

        return masks;
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



    #region expand
    private static List<Mask> ExpandRecurrentMask(
        RecurrentMask mask,
        ObjectId profileId,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        TimeSpan maskDuration = mask.EndTime - mask.StartTime;

        var dtos = RecurrenceExpansionService.GetOccurrences(
            mask.RecurrenceRule,
            mask.StartTime,
            mask.RecurrenceEnd,
            mask.TimeZone,
            Duration.FromTimeSpanExact(maskDuration),
            startTime,
            endTime)
            .Select(occurrenceStart => BuildMaskInstance(
                profileId,
                mask,
                occurrenceStart,
                occurrenceStart + maskDuration)
            ).ToList();

        return dtos;
    }

    /// Builds a transient (non-persisted) Event for one recurrence occurrence,
    /// copying all relevant fields from the master RecurrentEvent.
    private static Mask BuildMaskInstance(
        ObjectId profileId,
        RecurrentMask master,
        DateTimeOffset occurrenceStart,
        DateTimeOffset occurrenceEnd)
    {
        // Use compact ISO-8601 UTC instant as the instance identifier —
        // uniquely identifies this slot within the recurrence series.
        var instanceId = occurrenceStart.UtcDateTime.ToString("yyyyMMddTHHmmssZ");

        return new Mask(
            profileId,
            occurrenceStart,
            occurrenceEnd,
            master.Title,
            instanceId
        );
    }

    private static List<Mask> SubstituteWithInstances(List<Mask> masks, List<Mask> recurrenceMasks)
    {
        if (recurrenceMasks.Count > 0)
        {
            var overriddenInstanceIds = masks
                .Where(m => m.RecurrencyInstanceId != null)
                .Select(m => m.RecurrencyInstanceId)
                .ToHashSet();

            recurrenceMasks = recurrenceMasks
                .Where(m => m.RecurrencyInstanceId == null || !overriddenInstanceIds.Contains(m.RecurrencyInstanceId))
                .ToList();
        }
        return masks.Concat(recurrenceMasks).ToList();
    }


    #endregion


}
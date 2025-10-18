using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Users;
using Core.DTO.ProfileAPI;
using MongoDB.Bson;
using Core.Model.Profiles;
using Core.Components.MessageQueue;
using Core.Model.Notifications;


namespace Core.Services.Profiles;

public class ProfileService(
    MongoDbService dbService,
    ProfileDetailsService profileDetailsService,
    ProfileTagService profileTagService)
//MessageQueueService messageService)
{
    private readonly CollectionName profileCollection = CollectionName.Profiles;

    #region modify
    public async Task<Profile> CreateAsync(string tag, string name, IClientSessionHandle session)
    {
        var profile = new Profile(tag, name);
        // this function populates the in-memory profile object
        await dbService.CreateOneAsync(profileCollection, profile, session);

        await profileDetailsService.CreateAsync(profile, session);
        await profileTagService.CreateAsync(profile, session);

        return profile;
    }

    public async Task AddUserAsync(Profile profile, User user, IClientSessionHandle session)
    {
        await profileDetailsService.AddUser(profile.Id, user, session);
    }

    public async Task<RetrieveProfileResponseDto> Update(User user, UpdateProfileRequestDto updateDto)
    {
        var profileId = new ObjectId(updateDto.ProfileId);

        var updatedDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var updates = new List<UpdateDefinition<Profile>>();
            Profile? profile = null;
            User? updatedUser = null;

            if (updateDto.Name != null)
            {
                updates.Add(Builders<Profile>.Update.Set(p => p.Name, updateDto.Name));
            }

            if (updateDto.Tag != null)
            {
                updates.Add(Builders<Profile>.Update.Set(p => p.Tag, updateDto.Tag));
                await profileTagService.Update(profileId, updateDto.Tag, session);
            }

            if (updates.Count > 0)
            {
                var updateDefinition = Builders<Profile>.Update.Combine(updates);
                profile = await dbService.FindOneByIdAndUpdateAsync(profileCollection, profileId, updateDefinition, session: session);
            }

            if (updateDto.Color != null)
            {
                updatedUser = await SetProfileColor(user, profileId, updateDto.Color.Value, session);
            }

            if (updates.Count > 0 || updateDto.Color != null) // something has been changed
            {
                var notification = new Notification(profileId, NotificationType.UpdateProfile);
                //await messageService.SendNotificationAsync(notification);
            }

            var userProfile = updatedUser?.Profiles.FirstOrDefault(p => p.ProfileId == profileId);

            return profile != null
                ? new RetrieveProfileResponseDto(profile, userProfile)
                : new RetrieveProfileResponseDto(profileId, userProfile!);
        });

        return updatedDto;
    }

    private async Task<User> SetProfileColor(User user, ObjectId profileId, long color, IClientSessionHandle session)
    {

        var options = new FindOneAndUpdateOptions<User>
        {
            ArrayFilters =
            [
                new JsonArrayFilterDefinition<BsonDocument>(
                    $"{{ 'profile.profileId': ObjectId('{profileId}') }}")
            ],
            IsUpsert = false,
            ReturnDocument = ReturnDocument.After
        };

        var result = await dbService.FindOneByIdAndUpdateAsync(
            CollectionName.Users,
            user.Id,
            Builders<User>.Update.Set("profiles.$[profile].color", color),
            session: session,
            options: options);

        return result;
    }

    #endregion

    #region retrieve
    public async Task<Profile> RetrieveProfileById(string id)
    {
        var profile = await dbService.RetrieveByIdAsync<Profile>(profileCollection, id);
        return profile;
    }

    public async Task<HashSet<RetrieveProfileResponseDto>> RetrieveMultipleProfileById(HashSet<string> profileIds)
    {
        var profiles = await RetrieveMultiple(profileIds);
        return profiles.Select(p => new RetrieveProfileResponseDto(p)).ToHashSet();
    }

    public async Task<HashSet<Profile>> RetrieveMultiple(HashSet<string> profileIds)
    {
        var profiles = await dbService.RetrieveMultipleByIdAsync<Profile>(profileCollection, profileIds);
        return [.. profiles];
    }

    #endregion
}
/*


    public Profile UpdateAsync(ProfileDto dto)
    {
        Profile profileToUpdate;
        try
        {
            profileToUpdate = RetrieveByHash(dto.Hash!);
            profileToUpdate.Update(dto);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error updating event", ex);
        }

        //TODO check for removed images

        return profileToUpdate;

    }

    public void SetEventRole(Event ev, Profile profile, EventRole role)
    {
        var profileEvent = profile.ProfileEvents.Find(pe => pe.Event.Id == ev.Id) ?? throw new KeyNotFoundException("ProfileEvent");

        profileEvent.Role = role;

        db.SaveChanges();
    }



} */
using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Users;
using Core.DTO.ProfileAPI;
using MongoDB.Bson;
using Core.Model.Profiles;
using Core.Components.MessageQueue;
using Core.Model.Notifications;
using Core.DTO.UserAPI;
using Core.Services.Util;
using Core.Services.Users;

namespace Core.Services.Profiles;

public class ProfileService(
    MongoDbService dbService,
    ProfileDetailsService profileDetailsService,
    ProfileTagService profileTagService,
    MessageQueueService messageService,
    UserClaimService userClaimService,
    UserProfileService userProfileService,
    IContextManager contextManager)
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

    public async Task<RetrieveDetailedProfileResponseDto> Update(UpdateProfileRequestDto updateDto)
    {
        var userId = new ObjectId(contextManager.GetUserId());
        var profileId = new ObjectId(updateDto.ProfileId);

        var updatedDto = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            UserProfile? userProfile = null;
            Profile? updatedProfile = await UpdateProfileAsync(updateDto, profileId, session);

            if (updateDto.Tag != null)
                await profileTagService.Update(profileId, updateDto.Tag, session);

            if (updateDto.Color != null)
                userProfile = await userProfileService.Update(userId, profileId, updateDto.Color.Value, session);

            var somethingChanged = updatedProfile != null || updateDto.Color != null;
            if (somethingChanged)
            {
                var notification = new Notification(profileId, NotificationType.UpdateProfile);
                await messageService.SendNotificationAsync(notification);
            } // else throw nothingChangedException

            updatedProfile ??= await RetrieveProfileById(updateDto.ProfileId);
            return new RetrieveDetailedProfileResponseDto(updatedProfile, userProfile, null);
        });

        return updatedDto;
    }

    private async Task<Profile?> UpdateProfileAsync(UpdateProfileRequestDto updateDto, ObjectId profileId, IClientSessionHandle session)
    {
        Profile? profile = null;

        var updates = new List<UpdateDefinition<Profile>>();

        if (updateDto.Name != null)
            updates.Add(Builders<Profile>.Update.Set(p => p.Name, updateDto.Name));

        if (updateDto.Tag != null)
            updates.Add(Builders<Profile>.Update.Set(p => p.Tag, updateDto.Tag));

        if (updates.Count > 0)
        {
            var updateDefinition = Builders<Profile>.Update.Combine(updates);
            profile = await dbService.FindOneByIdAndUpdateAsync(profileCollection, profileId, updateDefinition, session: session);
        }

        return profile;
    }

    #endregion

    #region retrieve
    public async Task<Profile> RetrieveProfileById(string id)
    {
        var profile = await dbService.RetrieveByIdAsync<Profile>(profileCollection, id);
        return profile;
    }

    // for rtupdates
    public async Task<RetrieveDetailedProfileResponseDto> RetrieveDetailedProfileById(string profileId)
    {
        var userId = new ObjectId(contextManager.GetUserId());

        var profile = await dbService.RetrieveByIdAsync<Profile>(profileCollection, profileId);
        var userProfile = await userProfileService.RetrieveFromUserAndProfile(userId, profile.Id);
        var userClaims = await userClaimService.RetrieveFromUserAndProfile(userId, profile.Id);

        return new RetrieveDetailedProfileResponseDto(profile, userProfile, userClaims);
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
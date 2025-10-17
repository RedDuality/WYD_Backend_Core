using Core.Components.Database;
using MongoDB.Driver;
using Core.DTO.UserAPI;
using MongoDB.Bson;
using Core.Model.Users;
using Core.Model.Profiles;

namespace Core.Services.Users;

public class DeviceService(MongoDbService dbService)
{
    private readonly CollectionName userCollection = CollectionName.Users;

    public async Task AddDevice(User user, StoreFcmTokenRequestDto requestDto)
    {
        var device = new Device(platform: requestDto.Platform, fcmToken: requestDto.FcmToken);
        var deviceUpdate = Builders<User>.Update.AddToSet(u => u.Devices, device);
        await dbService.UpdateOneByIdAsync(userCollection, user.Id, deviceUpdate, setUpdatedAtDate: false);
    }

    public async Task RemoveDevice(ObjectId userId, string fcmToken)
    {
        var deviceUpdate = Builders<User>.Update.PullFilter(
            u => u.Devices,
            d => d.FcmToken.Equals(fcmToken, StringComparison.CurrentCultureIgnoreCase)
        );
        await dbService.UpdateOneByIdAsync(userCollection, userId, deviceUpdate, setUpdatedAtDate: false);
    }

    public async Task<Dictionary<string, ObjectId>> GetProfilesDevicesTokens(List<ObjectId> profileIds)
    {
        var profileDetails = await dbService.RetrieveMultipleAsync(
                    CollectionName.ProfileDetails,
                    Builders<ProfileDetails>.Filter.In(p => p.ProfileId, profileIds)
                );
        var userIds = profileDetails.SelectMany(pd => pd.Users).Select(pu => pu.UserId).ToHashSet();

        var users = await dbService.RetrieveMultipleAsync(
            userCollection,
            Builders<User>.Filter.In(u => u.Id, userIds)
        );

        // Create the Token -> User ID Dictionary
        var tokensWithUserIds = new Dictionary<string, ObjectId>();

        foreach (var user in users)
        {
            foreach (var device in user.Devices)
            {
                if (!string.IsNullOrEmpty(device.FcmToken))
                {
                    // We use TryAdd to handle cases where multiple users might coincidentally
                    // share a token (or if a token is registered for multiple users in error,
                    // we pick the first User ID encountered).
                    tokensWithUserIds.TryAdd(device.FcmToken, user.Id);
                }
            }
        }

        return tokensWithUserIds;
    }
}

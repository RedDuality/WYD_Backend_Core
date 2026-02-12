using Core.Model.Masks;
using Core.Model.MediaStorage;
using Core.Model.Profiles;
using Core.Model.Users;
using MongoDB.Bson;

namespace Core.Components.Database;

public class MongoDbInitializer(
    MongoDbInitializerService initService,
    Action<string>? logger = null
    )
{
    private readonly Action<string> log = logger ?? Console.WriteLine;

    public async Task InitAsync()
    {
        log("Initializing MongoDB collections...");

        await initService.Init();

        await InizialiseUsersAsync();
        await InitialiseProfilesAsync();
        await InitialiseMasksAsync();
        await InitialiseEventsAsync();
        await InitialiseCommunitiesAsync();

        log("MongoDB collection initialization complete.");
    }

    private async Task InizialiseUsersAsync()
    {
        // User
        await initService.InitializeCollectionAsync(CollectionName.Users, "_id");
        // create and retrieve accounts/users
        await initService.CreateCompoundIndexAsync<User>(
            CollectionName.Users,
            [("_id", 1), ("accounts.uid", 1)],
            isUnique: true
        );

        await initService.InitializeCollectionAsync(CollectionName.UserProfiles, "userId");
        // save and retrieve the user profiles
        await initService.CreateCompoundIndexAsync<UserProfile>(
            CollectionName.UserProfiles,
            [("userId", 1), ("profileId", 1)],
            isUnique: true
        );

        await initService.InitializeCollectionAsync(CollectionName.UserClaims, "userId");
        // save and retrieve the user's claims
        await initService.CreateCompoundIndexAsync<UserClaims>(
            CollectionName.UserClaims,
            [("userId", 1), ("profileId", 1)],
            isUnique: true
        );
    }
    private async Task InitialiseProfilesAsync()
    {
        await initService.InitializeCollectionAsync(CollectionName.Profiles, "_id");

        await initService.InitializeCollectionAsync(CollectionName.ProfileTags, "_id", doNotShard: true);
        await initService.CreateIndexAsync<ProfileTag>(CollectionName.ProfileTags, "tag", true);
        await initService.CreateIndexAsync<ProfileTag>(CollectionName.ProfileTags, "profileId", true);

        await initService.InitializeCollectionAsync(CollectionName.ProfileDetails, "profileId");

        // Event Join
        await initService.InitializeCollectionAsync(CollectionName.ProfileEvents, "profileId");
        // retrieveEvents
        await initService.CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventEndTime", 1), ("eventStartTime", 1)]
        );
        // create profileEvent, ensure uniqueness
        await initService.CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventId", 1)],
            isUnique: true
        );
        // propagate updates
        await initService.CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventId", 1), ("eventUpdatedAt", -1)]
        );
        // retrieve updates
        await initService.CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("updatedAt", 1)]
        );

        // collection join
        await initService.InitializeCollectionAsync(CollectionName.ProfileCommunities, "profileId");
        await initService.CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "communityUpdatedAt");
        await initService.CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "otherProfileId");
        await initService.CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "communityId");

        // mask imports
        await initService.InitializeCollectionAsync(CollectionName.MaskProfileImports, "maskProfileImports");
    }
    private async Task InitialiseMasksAsync()
    {
        // Masks
        await initService.InitializeCollectionAsync(CollectionName.Masks, "profileId");

        // retrieve all
        await initService.CreateCompoundIndexAsync<Mask>(
            CollectionName.Masks,
            [("profileId", 1), ("startTime", 1), ("endTime", 1)]
        );

        // retrieve, update and delete one
        await initService.CreateCompoundIndexAsync<Mask>(
            CollectionName.Masks,
            [("profileId", 1), ("_id", 1)],
            isUnique: true
        );

        // create/update/retrieve/delete Event's Mask
        await initService.CreateCompoundIndexAsync<Mask>(
            CollectionName.Masks,
            [("profileId", 1), ("eventId", 1)],
            isUnique: true,
            partialFilter: new BsonDocument("eventId", new BsonDocument("$exists", true))
        );

    }
    private async Task InitialiseEventsAsync()
    {
        // Events
        await initService.InitializeCollectionAsync(CollectionName.Events, "_id");

        await initService.InitializeCollectionAsync(CollectionName.EventDetails, "eventId");

        await initService.InitializeCollectionAsync(CollectionName.EventMedia, "parentId");
        await initService.CreateIndexAsync<Media>(CollectionName.EventMedia, "creationDate");

        await initService.InitializeCollectionAsync(CollectionName.EventProfiles, "eventId");
        await initService.CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.EventProfiles,
            [("eventId", 1), ("profileId", 1)],
            isUnique: true
        );
    }
    private async Task InitialiseCommunitiesAsync()
    {
        // Community
        await initService.InitializeCollectionAsync(CollectionName.Communities, "_id");

        await initService.InitializeCollectionAsync(CollectionName.Groups, "communityId");

        await initService.InitializeCollectionAsync(CollectionName.CommunityProfiles, "communityId");
    }

}

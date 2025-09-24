namespace Core.Components.Database;

public sealed class CollectionName
{
    public string Name { get; }
    private CollectionName(string name) => Name = name;

    public static readonly CollectionName Users = new("Users");
    public static readonly CollectionName Profiles = new("Profiles");
    public static readonly CollectionName ProfileDetails = new("ProfileDetails");
    public static readonly CollectionName ProfileEvents = new("ProfileEvents");
    public static readonly CollectionName ProfileCommunities = new("ProfileCommunities");

    public static readonly CollectionName ProfileTags = new("ProfileTags");

    public static readonly CollectionName Events = new("Events");
    public static readonly CollectionName EventDetails = new("EventDetails");
    public static readonly CollectionName EventMedia = new("EventMedia");
    public static readonly CollectionName EventProfiles = new("EventProfiles");

    public static readonly CollectionName Communities = new("Communities");
    public static readonly CollectionName Groups = new("Groups");
    public static readonly CollectionName CommunityProfiles = new("CommunityProfiles");

    public override string ToString() => Name;
}
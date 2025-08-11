namespace Core.Services.Database;

public sealed class CollectionName
{
    public string Name { get; }

    private CollectionName(string name) => Name = name;

    public static readonly CollectionName Users = new("Users");
    public static readonly CollectionName Profiles = new("Profiles");
    public static readonly CollectionName ProfileDetails = new("ProfileDetails");
    public static readonly CollectionName ProfileEvents = new("ProfileEvents");
    public static readonly CollectionName EventProfiles = new("EventProfiles");
    public static readonly CollectionName Events = new("Events");
    public static readonly CollectionName Images = new("Images");

    public override string ToString() => Name;
}
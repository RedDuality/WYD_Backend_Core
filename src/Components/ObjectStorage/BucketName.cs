namespace Core.Components.ObjectStorage;

public sealed class BucketName
{
    public string Name { get; }
    private BucketName(string name) => Name = name;

    // S3 rules: lowercase, 3-63 chars, eventually only "-"
    // if you add one, remember to update MediaType on the frontend
    public static readonly BucketName Events = new("events");

    public static readonly BucketName Profiles = new("profiles");

    public override string ToString() => Name;
}
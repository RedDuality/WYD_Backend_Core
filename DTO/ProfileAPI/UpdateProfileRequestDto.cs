namespace Core.DTO.ProfileAPI;

public class UpdateProfileRequestDto
{
    public required string ProfileId { get; set; }
    public string? Tag { get; set; }
    public string? Name { get; set; }

    public long? Color { get; set; }

    // Parameterless constructor for deserialization
    public UpdateProfileRequestDto() { }
}
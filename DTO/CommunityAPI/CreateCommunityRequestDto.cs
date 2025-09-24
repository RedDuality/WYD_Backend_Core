using Core.Model.Communities;

namespace Core.DTO.CommunityAPI;

public class CreateCommunityRequestDto
{
    required public string Name { get; set; }

    //public string? Description { get; set; }
    public CommunityType? Type { get; set; }

    public List<string> ProfileIds { get; set; } = [];

    // Parameterless constructor for deserialization
    public CreateCommunityRequestDto() { }
}

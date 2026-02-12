namespace Core.DTO.UserAPI;

public class StoreFcmTokenRequestDto
{
    public required string Platform { get; set; }
    public required string FcmToken { get; set; }

    public StoreFcmTokenRequestDto() { }
}
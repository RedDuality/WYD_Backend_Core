namespace Core.DTO.Model;

public class UserEventDto
{
    public int UserId { get; set; }
    public bool Confirmed { get ; set; } = false;
}
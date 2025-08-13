namespace Core.Model.Dto;

public class ProfileDto(Profile p)
{
    public String hash = p.Id.ToString();
    public String tag = p.Tag;
    public String name = p.Name;

}
namespace Core.Model.Dto;

public class ProfileDto(Profile p)
{
    public string hash = p.Id.ToString();
    public string tag = p.Tag;
    public string name = p.Name;

}
using Core.Model;

namespace Core.DTO.Model;

public class ProfileDto(Profile p)
{
    public string hash = p.Id.ToString();
    public string tag = p.Tag;
    public string name = p.Name;

}
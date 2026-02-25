using Core.Model.Profiles;

namespace Core.DTO.UserAPI;

public class ExternalImportDto(ExternalImport import)
{
    public string ImportedAccount { get; set; } = import.ImportedAccountEmail;
    public string ImportType { get; set; } = import.ImportType.ToString();
}

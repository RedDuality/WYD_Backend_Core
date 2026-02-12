using Core.Components.Database;
using Core.Model.Masks;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Driver;


namespace Core.Services.Masks;

public class ImportedProfilesService(
    MongoDbService dbService
)
{
    private readonly CollectionName MaskProfileImportsCollection = CollectionName.MaskProfileImports;

    public async Task<MaskProfileImports> CreateAsync(Profile profile, IClientSessionHandle session)
    {
        var imports = new MaskProfileImports(profile.Id);
        await dbService.CreateOneAsync(MaskProfileImportsCollection, imports, session: session);
        return imports;
    }

    public async Task<HashSet<ObjectId>> GetImportedProfiles(ObjectId profileId)
    {
        var filter = Builders<MaskProfileImports>.Filter.Eq(i => i.ProfileId, profileId);
        var imports = await dbService.RetrieveAsync(MaskProfileImportsCollection, filter);

        return imports.importedProfiles;
    }
}
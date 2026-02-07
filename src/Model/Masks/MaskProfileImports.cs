using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Masks;

public class MaskProfileImports(ObjectId profileId) : BaseEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId = profileId;

    [BsonElement("importedProfiles")]
    [BsonIgnoreIfDefault]
    public HashSet<ObjectId> importedProfiles = [];

    [BsonElement("importedBy")]
    [BsonIgnoreIfDefault]
    public HashSet<ObjectId> importedBy = [];
}


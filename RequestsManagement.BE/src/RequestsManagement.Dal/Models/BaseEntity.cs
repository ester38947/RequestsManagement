using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// מודל בסיס ל-Mongo. Id כ-ObjectId המיוצג כמחרוזת; timestamps תמיד UTC.
/// </summary>
public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

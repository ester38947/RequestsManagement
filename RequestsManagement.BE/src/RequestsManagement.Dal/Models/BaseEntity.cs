using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// Base model for Mongo. Id is an ObjectId represented as a string; timestamps are always UTC.
/// </summary>
public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

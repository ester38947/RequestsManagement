using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// An audit record for every request status change (append-only) in the "statusAudit" collection.
/// Written in the same atomic operation (transaction) as the status update.
/// </summary>
public sealed class StatusAuditEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    /// <summary>Id of the request that changed.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string RequestId { get; set; } = default!;

    public RequestStatus PreviousStatus { get; set; }

    public RequestStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; }

    /// <summary>Who performed the change. Defaults to "system" until a user identity is added.</summary>
    public string ChangedBy { get; set; } = "system";
}

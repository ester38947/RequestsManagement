using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// רשומת audit לכל שינוי סטטוס של פנייה (append-only) ב-collection "statusAudit".
/// נכתבת באותה פעולה אטומית (transaction) עם עדכון הסטטוס.
/// </summary>
public sealed class StatusAuditEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    /// <summary>מזהה הפנייה שהשתנתה.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string RequestId { get; set; } = default!;

    public RequestStatus PreviousStatus { get; set; }

    public RequestStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; }

    /// <summary>מי ביצע את השינוי. ברירת מחדל "system" עד שתתווסף זהות משתמש.</summary>
    public string ChangedBy { get; set; } = "system";
}

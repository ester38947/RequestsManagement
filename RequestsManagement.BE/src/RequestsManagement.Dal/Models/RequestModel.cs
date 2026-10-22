using MongoDB.Bson.Serialization.Attributes;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// מסמך פנייה ב-collection "requests".
/// Enums מאוחסנים כמחרוזות לקריאוּת; Version משמש ל-Optimistic Concurrency.
/// </summary>
public sealed class RequestModel : BaseEntity
{
    public string Title { get; set; } = default!;

    public string OrganizationName { get; set; } = default!;

    // נשמר כ-Int32 (ברירת המחדל ל-enum) כדי שמיון לפי Status/Priority ישקף את סדר ה-enum
    // ולא סדר אלפביתי של שמות. ערכי ה-enum מסודרים לפי חומרה/זרימה.
    public RequestStatus Status { get; set; }

    public RequestPriority Priority { get; set; }

    public string? AssignedTo { get; set; }

    /// <summary>RowVersion לזיהוי עדכון מתחרה (Optimistic Concurrency). מתחיל ב-0, עולה בכל עדכון.</summary>
    public int Version { get; set; }
}

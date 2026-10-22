using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// גוף בקשת עדכון סטטוס מרוכז (PATCH /api/requests/bulk-status).
/// סטטוס יעד אחד לכל הפניות; לכל פנייה הגרסה שהלקוח ראה (Optimistic Concurrency פר-פנייה).
/// </summary>
public sealed class BulkUpdateStatusRequest
{
    public const int MaxItems = 100;

    public RequestStatus? NewStatus { get; set; }

    public List<BulkUpdateStatusItem>? Items { get; set; }

    /// <summary>מי מבצע את השינוי (לרשומות ה-audit). אופציונלי; ברירת מחדל "system".</summary>
    public string? ChangedBy { get; set; }
}

/// <summary>פנייה בודדת בבקשת bulk.</summary>
public sealed class BulkUpdateStatusItem
{
    public string? Id { get; set; }

    public int? ExpectedVersion { get; set; }
}

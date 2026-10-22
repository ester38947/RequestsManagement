using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// גוף בקשת עדכון סטטוס (PATCH /api/requests/{id}/status).
/// ExpectedVersion חובה — זו הגרסה שהלקוח ראה; אם היא שונה מהגרסה ב-DB → 409 Conflict.
/// השדות nullable כדי ששדה חסר ייתפס בוולידציה (400) ולא יקבל ברירת מחדל 0 בשקט
/// (שהייתה עוקפת את בדיקת התחרותיות על פניות בגרסה 0).
/// </summary>
public sealed class UpdateRequestStatusRequest
{
    public RequestStatus? NewStatus { get; set; }

    public int? ExpectedVersion { get; set; }

    /// <summary>מי מבצע את השינוי (לרשומת ה-audit). אופציונלי; ברירת מחדל "system".</summary>
    public string? ChangedBy { get; set; }
}

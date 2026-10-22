using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Dal.Interfaces;

/// <summary>גישה לנתוני פניות. כל הפעולות א-סינכרוניות ומקבלות CancellationToken.</summary>
public interface IRequestRepository
{
    /// <summary>שליפת דף פניות לפי קריטריוני סינון/חיפוש/מיון, יחד עם סך ההתאמות.</summary>
    Task<(IReadOnlyList<RequestModel> Items, long TotalCount)> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default);

    /// <summary>שליפת פנייה לפי מזהה; null אם לא קיימת.</summary>
    Task<RequestModel?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>שליפת מספר פניות בשאילתה אחת ($in) — משמש ל-bulk במקום N קריאות.</summary>
    Task<IReadOnlyList<RequestModel>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);

    /// <summary>
    /// עדכון סטטוס אטומי עם Optimistic Concurrency + כתיבת audit באותה transaction.
    /// העדכון מותנה ב-Version (וגם בסטטוס הנוכחי שנבדק) — אם המסמך השתנה בינתיים, לא מתבצע דבר
    /// ומוחזר null (עדכון מתחרה). בהצלחה: Status חדש, UpdatedAt=UtcNow, Version++ ורשומת audit.
    /// </summary>
    Task<RequestModel?> UpdateStatusWithAuditAsync(
        string id,
        int expectedVersion,
        RequestStatus expectedCurrentStatus,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>
    /// ספירה לפי סטטוס ולפי עדיפות במעבר אחד ($facet) — שתי הספירות מאותה קריאה, ולכן עקביות זו עם זו.
    /// </summary>
    Task<RequestCounts> GetCountsAsync(CancellationToken ct = default);
}

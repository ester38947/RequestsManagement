using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Interfaces;

/// <summary>לוגיקת פניות: ולידציה, תזמור ה-repositories, והחזרת DTOs בלבד.</summary>
public interface IRequestService
{
    Task<PagedResultDto<RequestListItemDto>> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default);

    Task<RequestsSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>פנייה לפי מזהה; null אם לא קיימת. מזהה לא תקין → ValidationException (400).</summary>
    Task<RequestListItemDto?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// עדכון סטטוס עם בדיקת מעבר חוקי ו-Optimistic Concurrency.
    /// 404 – לא קיימת; 400 – קלט/מעבר לא חוקי; 409 – עדכון מתחרה.
    /// </summary>
    Task<RequestListItemDto> UpdateStatusAsync(
        string id,
        UpdateRequestStatusRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// עדכון סטטוס מרוכז (עד 100) עם Partial Success: כל פנייה בנפרד (transaction משלה),
    /// ודוח תוצאה פר-פנייה. קלט לא תקין ברמת הבקשה → ValidationException (400).
    /// </summary>
    Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(
        BulkUpdateStatusRequest request,
        CancellationToken ct = default);

    /// <summary>היסטוריית שינויי סטטוס (מהחדש לישן); null אם הפנייה לא קיימת.</summary>
    Task<IReadOnlyList<RequestStatusHistoryItemDto>?> GetHistoryAsync(string id, CancellationToken ct = default);
}

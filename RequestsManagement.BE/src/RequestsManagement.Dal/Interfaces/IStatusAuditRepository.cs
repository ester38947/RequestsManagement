using RequestsManagement.Dal.Models;

namespace RequestsManagement.Dal.Interfaces;

/// <summary>קריאת היסטוריית שינויי סטטוס. הכתיבה מתבצעת בתוך ה-transaction של עדכון הסטטוס.</summary>
public interface IStatusAuditRepository
{
    /// <summary>היסטוריית פנייה, ממוינת מהחדש לישן.</summary>
    Task<IReadOnlyList<StatusAuditEntry>> GetByRequestIdAsync(string requestId, CancellationToken ct = default);
}

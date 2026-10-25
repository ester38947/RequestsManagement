using RequestsManagement.Dal.Models;

namespace RequestsManagement.Dal.Interfaces;

/// <summary>Reads status-change history. Writing happens inside the status-update transaction.</summary>
public interface IStatusAuditRepository
{
    /// <summary>A request's history, sorted newest to oldest.</summary>
    Task<IReadOnlyList<StatusAuditEntry>> GetByRequestIdAsync(string requestId, CancellationToken ct = default);
}

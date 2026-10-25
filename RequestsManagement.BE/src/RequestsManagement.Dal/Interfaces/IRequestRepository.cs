using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Dal.Interfaces;

/// <summary>Request data access. All operations are asynchronous and take a CancellationToken.</summary>
public interface IRequestRepository
{
    /// <summary>Fetches a page of requests by filter/search/sort criteria, together with the total match count.</summary>
    Task<(IReadOnlyList<RequestModel> Items, long TotalCount)> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default);

    /// <summary>Fetches a request by id; null if it does not exist.</summary>
    Task<RequestModel?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Fetches several requests in one query ($in); used by bulk instead of N round-trips.</summary>
    Task<IReadOnlyList<RequestModel>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);

    /// <summary>
    /// Atomic status update with optimistic concurrency + an audit write in the same transaction.
    /// The update is conditioned on Version (and the current status) — if the document changed in the
    /// meantime, nothing happens and null is returned (concurrent update). On success: new Status,
    /// UpdatedAt=UtcNow, Version++ and an audit record.
    /// </summary>
    Task<RequestModel?> UpdateStatusWithAuditAsync(
        string id,
        int expectedVersion,
        RequestStatus expectedCurrentStatus,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Counts by status and by priority in one pass ($facet) — both counts come from the same call, so they stay consistent.
    /// </summary>
    Task<RequestCounts> GetCountsAsync(CancellationToken ct = default);
}

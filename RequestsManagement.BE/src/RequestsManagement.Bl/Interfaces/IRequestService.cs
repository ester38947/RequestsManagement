using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Interfaces;

/// <summary>Request logic: validation, orchestrating the repositories, and returning DTOs only.</summary>
public interface IRequestService
{
    Task<PagedResultDto<RequestListItemDto>> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default);

    Task<RequestsSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>A request by id; null if it does not exist. An invalid id -> ValidationException (400).</summary>
    Task<RequestListItemDto?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Status update with transition validity check and optimistic concurrency.
    /// 404 - not found; 400 - invalid input/transition; 409 - concurrent update.
    /// </summary>
    Task<RequestListItemDto> UpdateStatusAsync(
        string id,
        UpdateRequestStatusRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk status update (up to 100) with partial success: each request independently (its own
    /// transaction), with a per-item result report. Invalid request-level input -> ValidationException (400).
    /// </summary>
    Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(
        BulkUpdateStatusRequest request,
        CancellationToken ct = default);

    /// <summary>Status-change history (newest to oldest); null if the request does not exist.</summary>
    Task<IReadOnlyList<RequestStatusHistoryItemDto>?> GetHistoryAsync(string id, CancellationToken ct = default);
}

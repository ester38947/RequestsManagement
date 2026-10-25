using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// Bulk result report (partial success): each request is processed and recorded independently,
/// so the failure of one does not cancel the others.
/// </summary>
public sealed record BulkUpdateStatusResultDto(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkStatusItemResultDto> Items);

/// <summary>
/// Result for a single request.
/// Version: on success, the new version; on conflict/invalid transition, the current version
/// in the DB; on NotFound, null.
/// </summary>
public sealed record BulkStatusItemResultDto(
    string Id,
    BulkItemOutcome Outcome,
    int? Version,
    RequestStatus? CurrentStatus,
    string? Error);

public enum BulkItemOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    InvalidTransition = 3,
}

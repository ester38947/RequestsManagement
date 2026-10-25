using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Bl.Exceptions;

/// <summary>
/// Concurrent update (optimistic concurrency): the version the client sent is not the current one in the DB.
/// Mapped to 409 Conflict, including the current version and status so the FE can refresh.
/// </summary>
public sealed class ConcurrencyConflictException(string requestId, int currentVersion, RequestStatus currentStatus)
    : Exception(ValidationMessages.ConcurrencyConflict)
{
    public string RequestId { get; } = requestId;

    public int CurrentVersion { get; } = currentVersion;

    public RequestStatus CurrentStatus { get; } = currentStatus;
}

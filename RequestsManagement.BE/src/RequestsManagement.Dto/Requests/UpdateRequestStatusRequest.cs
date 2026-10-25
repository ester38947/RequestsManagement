using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// Body of a status-update request (PATCH /api/requests/{id}/status).
/// ExpectedVersion is required: it is the version the client saw; if it differs from the
/// version in the DB the result is 409 Conflict.
/// The fields are nullable so a missing field is caught by validation (400) instead of
/// silently defaulting to 0 (which would bypass the concurrency check for version-0 requests).
/// </summary>
public sealed class UpdateRequestStatusRequest
{
    public RequestStatus? NewStatus { get; set; }

    public int? ExpectedVersion { get; set; }

    /// <summary>Who performed the change (for the audit record). Optional; defaults to "system".</summary>
    public string? ChangedBy { get; set; }
}

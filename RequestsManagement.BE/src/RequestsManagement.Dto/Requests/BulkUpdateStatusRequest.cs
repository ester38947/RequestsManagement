using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// Body of a bulk status-update request (PATCH /api/requests/bulk-status).
/// One target status for all requests; each item carries the version the client saw
/// (optimistic concurrency, per item).
/// </summary>
public sealed class BulkUpdateStatusRequest
{
    public const int MaxItems = 100;

    public RequestStatus? NewStatus { get; set; }

    public List<BulkUpdateStatusItem>? Items { get; set; }

    /// <summary>Who performed the change (for audit records). Optional; defaults to "system".</summary>
    public string? ChangedBy { get; set; }
}

/// <summary>A single request inside a bulk request.</summary>
public sealed class BulkUpdateStatusItem
{
    public string? Id { get; set; }

    public int? ExpectedVersion { get; set; }
}

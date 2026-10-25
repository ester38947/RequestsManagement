using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>An entry in a request's status-change history.</summary>
public sealed record RequestStatusHistoryItemDto(
    string Id,
    string RequestId,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    DateTime ChangedAt,
    string ChangedBy);

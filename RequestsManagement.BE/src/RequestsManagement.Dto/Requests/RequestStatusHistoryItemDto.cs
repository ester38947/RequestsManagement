using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>פריט בהיסטוריית שינויי הסטטוס של פנייה.</summary>
public sealed record RequestStatusHistoryItemDto(
    string Id,
    string RequestId,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    DateTime ChangedAt,
    string ChangedBy);

using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// סיכום aggregations על הפניות: ספירה לפי סטטוס ולפי עדיפות (Task 2.6).
/// </summary>
public sealed record RequestsSummaryDto(
    long Total,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority);

public sealed record StatusCountDto(RequestStatus Status, long Count);

public sealed record PriorityCountDto(RequestPriority Priority, long Count);

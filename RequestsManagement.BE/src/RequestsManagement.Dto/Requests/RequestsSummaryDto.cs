using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// Aggregations summary over requests: counts by status and by priority.
/// </summary>
public sealed record RequestsSummaryDto(
    long Total,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority);

public sealed record StatusCountDto(RequestStatus Status, long Count);

public sealed record PriorityCountDto(RequestPriority Priority, long Count);

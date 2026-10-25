using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>Result of the summary aggregation: counts by status and by priority.</summary>
public sealed record RequestCounts(
    IReadOnlyDictionary<RequestStatus, long> ByStatus,
    IReadOnlyDictionary<RequestPriority, long> ByPriority);

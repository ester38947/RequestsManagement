using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>תוצאת ה-aggregation של הסיכום: ספירה לפי סטטוס ולפי עדיפות.</summary>
public sealed record RequestCounts(
    IReadOnlyDictionary<RequestStatus, long> ByStatus,
    IReadOnlyDictionary<RequestPriority, long> ByPriority);

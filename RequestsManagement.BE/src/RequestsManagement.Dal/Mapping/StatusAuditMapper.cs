using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Dal.Mapping;

/// <summary>מיפוי ידני של רשומות audit ל-DTO.</summary>
public static class StatusAuditMapper
{
    public static RequestStatusHistoryItemDto ToHistoryItemDto(StatusAuditEntry entry) => new(
        Id: entry.Id,
        RequestId: entry.RequestId,
        PreviousStatus: entry.PreviousStatus,
        NewStatus: entry.NewStatus,
        ChangedAt: entry.ChangedAt,
        ChangedBy: entry.ChangedBy);

    public static IReadOnlyList<RequestStatusHistoryItemDto> ToHistoryItemDtos(IEnumerable<StatusAuditEntry> entries) =>
        entries.Select(ToHistoryItemDto).ToList();
}

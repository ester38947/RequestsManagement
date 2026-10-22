using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Dal.Mapping;

/// <summary>
/// מיפוי ידני ושקוף בין מודל ה-DB ל-DTOs (ללא AutoMapper).
/// </summary>
public static class RequestMapper
{
    public static RequestListItemDto ToListItemDto(RequestModel model) => new(
        Id: model.Id,
        Title: model.Title,
        OrganizationName: model.OrganizationName,
        Status: model.Status,
        Priority: model.Priority,
        AssignedTo: model.AssignedTo,
        CreatedAt: model.CreatedAt,
        UpdatedAt: model.UpdatedAt,
        Version: model.Version);

    public static IReadOnlyList<RequestListItemDto> ToListItemDtos(IEnumerable<RequestModel> models) =>
        models.Select(ToListItemDto).ToList();
}

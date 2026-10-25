using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Dal.Mapping;

/// <summary>
/// Manual, explicit mapping between the DB model and DTOs (no AutoMapper).
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

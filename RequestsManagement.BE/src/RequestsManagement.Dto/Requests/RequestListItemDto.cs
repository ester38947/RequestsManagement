using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>A request list item (the fields the table needs).</summary>
public sealed record RequestListItemDto(
    string Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version);

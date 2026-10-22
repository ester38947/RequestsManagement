using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>פריט ברשימת פניות (שדות הדרושים לטבלה).</summary>
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

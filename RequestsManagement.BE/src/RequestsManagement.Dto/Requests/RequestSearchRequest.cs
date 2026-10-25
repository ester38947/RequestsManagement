using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// Request query parameters: paging + combined filtering + text search + sorting.
/// Bound as [FromQuery] in the controller.
/// </summary>
public sealed class RequestSearchRequest
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>Allowed sort fields (whitelist). Maps a logical name to the Mongo field name.</summary>
    public static readonly IReadOnlyDictionary<string, string> SortableFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAt"] = "CreatedAt",
            ["updatedAt"] = "UpdatedAt",
            ["priority"] = "Priority",
            ["status"] = "Status",
            ["organizationName"] = "OrganizationName",
            ["title"] = "Title",
        };

    // --- Paging ---
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    // --- Filtering (combined with AND) ---
    public RequestStatus? Status { get; set; }

    public RequestPriority? Priority { get; set; }

    public string? OrganizationName { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    // --- Text search (Title + OrganizationName) ---
    public string? Search { get; set; }

    // --- Sorting ---
    public string SortBy { get; set; } = "createdAt";

    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

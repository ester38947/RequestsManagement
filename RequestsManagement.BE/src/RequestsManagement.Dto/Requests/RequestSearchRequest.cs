using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// פרמטרי שליפה של פניות: דפדוף + סינון משולב + חיפוש טקסט + מיון.
/// מגיע כ-[FromQuery] ב-controller.
/// </summary>
public sealed class RequestSearchRequest
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>שדות מותרים למיון (whitelist). מיפוי שם לוגי → שם שדה ב-Mongo.</summary>
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

    // --- דפדוף ---
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    // --- סינון (משתלב ב-AND) ---
    public RequestStatus? Status { get; set; }

    public RequestPriority? Priority { get; set; }

    public string? OrganizationName { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    // --- חיפוש טקסט (Title + OrganizationName) ---
    public string? Search { get; set; }

    // --- מיון ---
    public string SortBy { get; set; } = "createdAt";

    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

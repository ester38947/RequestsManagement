namespace RequestsManagement.Dto.Paging;

/// <summary>
/// תוצאת דף בשליפה server-side: הפריטים של הדף הנוכחי + מטא-נתוני דפדוף.
/// לעולם לא מחזיר את כל ה-collection לזיכרון.
/// </summary>
public sealed class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>מספר הפריטים הכולל שתואם את הסינון (לפני דפדוף).</summary>
    public long TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResultDto<T> Create(IReadOnlyList<T> items, long totalCount, int page, int pageSize) => new()
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
    };
}

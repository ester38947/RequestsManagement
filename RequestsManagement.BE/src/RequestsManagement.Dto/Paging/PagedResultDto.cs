namespace RequestsManagement.Dto.Paging;

/// <summary>
/// A page of a server-side query: the items of the current page plus paging metadata.
/// Never loads the whole collection into memory.
/// </summary>
public sealed class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total number of items matching the filter (before paging).</summary>
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

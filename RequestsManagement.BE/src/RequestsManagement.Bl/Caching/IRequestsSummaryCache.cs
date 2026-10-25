using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Caching;

/// <summary>
/// Cache for the aggregations summary. Registered as a Singleton.
/// </summary>
public interface IRequestsSummaryCache
{
    /// <summary>Returns the summary from the cache, or computes it via <paramref name="factory"/> and stores it.</summary>
    Task<RequestsSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> factory,
        CancellationToken ct = default);

    /// <summary>Invalidates the stored summary. Called after any data change that affects the counts.</summary>
    void Invalidate();
}

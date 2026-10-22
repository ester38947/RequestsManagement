using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.Bl.Caching;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.UnitTests.Caching;

public sealed class RequestsSummaryCacheTests : IDisposable
{
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());
    private readonly RequestsSummaryCache _cache;
    private int _computations;

    public RequestsSummaryCacheTests()
    {
        _cache = new RequestsSummaryCache(_memory, NullLogger<RequestsSummaryCache>.Instance);
    }

    public void Dispose() => _memory.Dispose();

    [Fact]
    public async Task SecondCall_IsServedFromCache()
    {
        var first = await _cache.GetOrCreateAsync(Compute);
        var second = await _cache.GetOrCreateAsync(Compute);

        Assert.Equal(1, _computations);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task Invalidate_ForcesRecompute()
    {
        await _cache.GetOrCreateAsync(Compute);

        _cache.Invalidate();
        var fresh = await _cache.GetOrCreateAsync(Compute);

        Assert.Equal(2, _computations);
        Assert.Equal(2, fresh.Total);
    }

    [Fact]
    public async Task InvalidateDuringComputation_DoesNotCacheStaleResult()
    {
        // חישוב שהתחיל לפני עדכון ומסתיים אחריו לא נשמר — אחרת הנתון הישן היה "חוזר לחיים" עד ה-expiration.
        var release = new TaskCompletionSource();
        var slow = _cache.GetOrCreateAsync(async _ =>
        {
            await release.Task;
            return Summary(total: -1);
        });

        _cache.Invalidate();
        release.SetResult();
        var resultForInFlightCaller = await slow;

        var next = await _cache.GetOrCreateAsync(Compute);

        Assert.Equal(-1, resultForInFlightCaller.Total); // הקורא שהמתין מקבל את מה שחושב...
        Assert.Equal(1, _computations);                  // ...אבל הקריאה הבאה מחשבת מחדש
        Assert.Equal(1, next.Total);                     // ולא מקבלת את הערך הישן מהמטמון.
    }

    [Fact]
    public async Task FailedComputation_IsNotCached()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cache.GetOrCreateAsync(_ => throw new InvalidOperationException("db down")));

        await _cache.GetOrCreateAsync(Compute);

        Assert.Equal(1, _computations);
    }

    private Task<RequestsSummaryDto> Compute(CancellationToken ct)
    {
        _computations++;
        return Task.FromResult(Summary(_computations));
    }

    private static RequestsSummaryDto Summary(long total) => new(total, [], []);
}

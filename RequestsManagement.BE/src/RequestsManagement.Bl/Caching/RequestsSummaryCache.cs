using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Caching;

/// <summary>
/// In-memory cache for the aggregations summary.
///
/// Why cache it: the summary scans the whole collection (a group over 100K+), is read on every screen
/// load, and only changes when a status changes — a high read/write ratio and an expensive computation.
///
/// Expiration: 60 seconds (absolute) — a safety net for changes that do not go through the application
/// (a direct DB update, or another server instance). On the normal path, invalidation is immediate.
///
/// Invalidation with a generation counter: every Invalidate advances the generation, and the key
/// includes it. So a computation that started before an update and finished after it is stored under an
/// old key that nobody reads — it does not "revive" stale data for 60 seconds (a read/write race).
///
/// Scale-out: the cache is per-instance. With multiple instances, Invalidate on one does not clear the
/// others, so another instance may serve a stale summary until expiration. For production:
/// IDistributedCache (Redis) with a shared key, or Redis pub/sub to broadcast invalidation. The
/// IRequestsSummaryCache interface allows swapping the implementation without changing the BL.
/// </summary>
public sealed class RequestsSummaryCache(IMemoryCache cache, ILogger<RequestsSummaryCache> logger)
    : IRequestsSummaryCache
{
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    private const string KeyPrefix = "requests:summary:g";

    private long _generation;

    public async Task<RequestsSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> factory,
        CancellationToken ct = default)
    {
        var generation = Interlocked.Read(ref _generation);
        var key = KeyFor(generation);

        if (cache.TryGetValue(key, out RequestsSummaryDto? cached) && cached is not null)
        {
            logger.LogDebug("Summary cache hit (generation {Generation})", generation);
            return cached;
        }

        logger.LogDebug("Summary cache miss (generation {Generation}) — computing", generation);
        var fresh = await factory(ct);

        // If an invalidation happened during the computation, the result may be stale, so do not store it
        // (the current caller still gets it; the next call will recompute).
        if (Interlocked.Read(ref _generation) == generation)
        {
            cache.Set(key, fresh, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Expiration });
        }
        else
        {
            logger.LogDebug("Summary invalidated during computation (generation {Generation}) — not caching", generation);
        }

        return fresh;
    }

    public void Invalidate()
    {
        var previous = Interlocked.Increment(ref _generation) - 1;
        cache.Remove(KeyFor(previous));
        logger.LogDebug("Summary cache invalidated (generation {Previous} -> {Current})", previous, previous + 1);
    }

    private static string KeyFor(long generation) => $"{KeyPrefix}{generation}";
}

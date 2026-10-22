using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Caching;

/// <summary>
/// מטמון in-memory לסיכום ה-aggregations.
///
/// למה לשמור במטמון: הסיכום סורק את כל ה-collection (group על 100K+), הוא נקרא בכל טעינת מסך,
/// והוא משתנה רק כשסטטוס משתנה — כלומר יחס קריאה/כתיבה גבוה וחישוב יקר.
///
/// Expiration: 60 שניות (absolute) — רשת ביטחון לשינויים שלא עוברים דרך האפליקציה
/// (עדכון ישיר ב-DB או מופע אחר של השרת). במסלול הרגיל ה-invalidation מיידי.
///
/// Invalidation עם generation counter: כל Invalidate מקדם את ה-generation, והמפתח כולל אותו.
/// כך חישוב שהתחיל לפני עדכון ומסתיים אחריו נשמר תחת מפתח ישן שאף אחד לא קורא —
/// ולא "מחזיר לחיים" נתון מיושן ל-60 שניות (race בין קריאה לכתיבה).
///
/// Scale-out: המטמון הוא per-instance. בריבוי מופעים, Invalidate במופע אחד לא מנקה את האחרים,
/// ואז מופע אחר עלול להגיש סיכום מיושן עד ה-expiration. לפרודקשן: IDistributedCache (Redis)
/// עם מפתח משותף, או Redis pub/sub להפצת invalidation. הממשק IRequestsSummaryCache מאפשר החלפה ללא שינוי ב-BL.
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

        // אם בוצעה invalidation בזמן החישוב — התוצאה עלולה להיות מיושנת, לא שומרים אותה
        // (הקורא הנוכחי עדיין מקבל אותה; הקריאה הבאה תחשב מחדש).
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

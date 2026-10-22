using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Caching;

/// <summary>
/// מטמון לסיכום ה-aggregations (Task 5.1/5.2). נרשם כ-Singleton.
/// </summary>
public interface IRequestsSummaryCache
{
    /// <summary>מחזיר את הסיכום מהמטמון, או מחשב דרך <paramref name="factory"/> ושומר.</summary>
    Task<RequestsSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> factory,
        CancellationToken ct = default);

    /// <summary>מבטל את הסיכום השמור. נקרא אחרי כל שינוי נתונים שמשפיע על הספירות.</summary>
    void Invalidate();
}

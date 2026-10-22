using RequestsManagement.Bl.Caching;
using RequestsManagement.Dal.Interfaces;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.UnitTests.Fakes;

internal sealed class FakeStatusAuditRepository : IStatusAuditRepository
{
    public Task<IReadOnlyList<StatusAuditEntry>> GetByRequestIdAsync(string requestId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StatusAuditEntry>>([]);
}

/// <summary>מטמון שלא שומר דבר, וסופר ביטולים — לבדיקה שה-service מבטל רק כשצריך.</summary>
internal sealed class CountingSummaryCache : IRequestsSummaryCache
{
    public int Invalidations { get; private set; }

    public Task<RequestsSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> factory,
        CancellationToken ct = default) => factory(ct);

    public void Invalidate() => Invalidations++;
}

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

/// <summary>A cache that stores nothing and counts invalidations — to verify the service invalidates only when needed.</summary>
internal sealed class CountingSummaryCache : IRequestsSummaryCache
{
    public int Invalidations { get; private set; }

    public Task<RequestsSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> factory,
        CancellationToken ct = default) => factory(ct);

    public void Invalidate() => Invalidations++;
}

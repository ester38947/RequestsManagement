using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Interfaces;
using RequestsManagement.Dal.Models;

namespace RequestsManagement.Dal.Repositories;

/// <summary>Reads status history (backed by the ix_requestId_changedAt index).</summary>
public sealed class StatusAuditRepository(AppMongoDbContext context) : IStatusAuditRepository
{
    private readonly IMongoCollection<StatusAuditEntry> _col = context.StatusAudit;

    public async Task<IReadOnlyList<StatusAuditEntry>> GetByRequestIdAsync(
        string requestId,
        CancellationToken ct = default) =>
        await _col
            .Find(Builders<StatusAuditEntry>.Filter.Eq(x => x.RequestId, requestId))
            .Sort(Builders<StatusAuditEntry>.Sort.Descending(x => x.ChangedAt))
            .ToListAsync(ct);
}

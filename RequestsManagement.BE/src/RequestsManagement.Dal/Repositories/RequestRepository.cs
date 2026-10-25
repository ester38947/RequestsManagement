using MongoDB.Bson;
using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;
using SortDirection = RequestsManagement.Dto.Paging.SortDirection;

namespace RequestsManagement.Dal.Repositories;

/// <summary>
/// Request data access over the Mongo fluent API.
/// Filtering via Builders.Filter + And; index-backed text search ($text); sorting limited to a
/// whitelist; skip/limit paging with a separate totalCount, so the whole collection is never loaded.
/// </summary>
public sealed class RequestRepository(AppMongoDbContext context) : Interfaces.IRequestRepository
{
    private readonly IMongoCollection<RequestModel> _col = context.Requests;
    private readonly IMongoCollection<StatusAuditEntry> _audit = context.StatusAudit;

    public async Task<RequestModel?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await _col.Find(Builders<RequestModel>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RequestModel>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _col.Find(Builders<RequestModel>.Filter.In(x => x.Id, ids)).ToListAsync(ct);
    }

    public async Task<RequestModel?> UpdateStatusWithAuditAsync(
        string id,
        int expectedVersion,
        RequestStatus expectedCurrentStatus,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken ct = default)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);

        // WithTransactionAsync retries automatically on a TransientTransactionError (e.g. a WriteConflict
        // between two concurrent transactions on the same document). On the retry the Version condition
        // no longer holds -> null -> 409, so even a DB-level race ends as a clear conflict, not a 500.
        return await session.WithTransactionAsync<RequestModel?>(
            async (s, token) =>
            {
                var now = DateTime.UtcNow;
                var f = Builders<RequestModel>.Filter;

                var filter = f.And(
                    f.Eq(x => x.Id, id),
                    f.Eq(x => x.Version, expectedVersion),
                    f.Eq(x => x.Status, expectedCurrentStatus));

                var update = Builders<RequestModel>.Update
                    .Set(x => x.Status, newStatus)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1);

                var updated = await _col.FindOneAndUpdateAsync(
                    s,
                    filter,
                    update,
                    new FindOneAndUpdateOptions<RequestModel> { ReturnDocument = ReturnDocument.After },
                    token);

                if (updated is null)
                {
                    return null; // Concurrent update: do not write audit.
                }

                await _audit.InsertOneAsync(
                    s,
                    new StatusAuditEntry
                    {
                        RequestId = id,
                        PreviousStatus = expectedCurrentStatus,
                        NewStatus = newStatus,
                        ChangedAt = now,
                        ChangedBy = changedBy,
                    },
                    cancellationToken: token);

                return updated;
            },
            cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<RequestModel> Items, long TotalCount)> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default)
    {
        var (filter, hasFilter) = BuildFilter(request);
        var sort = BuildSort(request);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize;
        var skip = (page - 1) * pageSize;

        // Count and fetch the page in parallel to cut latency.
        var totalTask = hasFilter
            ? _col.CountDocumentsAsync(filter, cancellationToken: ct)
            : _col.EstimatedDocumentCountAsync(cancellationToken: ct);
        var itemsTask = _col
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);

        await Task.WhenAll(totalTask, itemsTask);

        return (await itemsTask, await totalTask);
    }

    public async Task<RequestCounts> GetCountsAsync(CancellationToken ct = default)
    {
        // A single pass over the collection instead of two separate aggregations (measured: ~280ms vs ~420ms on 100K).
        var pipeline = new[]
        {
            new BsonDocument("$facet", new BsonDocument
            {
                { "byStatus", GroupBy(nameof(RequestModel.Status)) },
                { "byPriority", GroupBy(nameof(RequestModel.Priority)) },
            }),
        };

        var cursor = await _col.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
        var result = await cursor.FirstOrDefaultAsync(ct) ?? new BsonDocument();

        return new RequestCounts(
            ToCounts<RequestStatus>(result, "byStatus"),
            ToCounts<RequestPriority>(result, "byPriority"));
    }

    private static (FilterDefinition<RequestModel> Filter, bool HasFilter) BuildFilter(RequestSearchRequest r)
    {
        var b = Builders<RequestModel>.Filter;
        var filters = new List<FilterDefinition<RequestModel>>();

        if (r.Status is { } status)
        {
            filters.Add(b.Eq(x => x.Status, status));
        }

        if (r.Priority is { } priority)
        {
            filters.Add(b.Eq(x => x.Priority, priority));
        }

        if (!string.IsNullOrWhiteSpace(r.OrganizationName))
        {
            filters.Add(b.Eq(x => x.OrganizationName, r.OrganizationName.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(r.AssignedTo))
        {
            filters.Add(b.Eq(x => x.AssignedTo, r.AssignedTo.Trim()));
        }

        if (r.CreatedFrom is { } from)
        {
            filters.Add(b.Gte(x => x.CreatedAt, from));
        }

        if (r.CreatedTo is { } to)
        {
            filters.Add(b.Lte(x => x.CreatedAt, to));
        }

        // Index-backed text search on Title + OrganizationName (text index).
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            filters.Add(b.Text(r.Search.Trim()));
        }

        return filters.Count == 0 ? (b.Empty, false) : (b.And(filters), true);
    }

    private static SortDefinition<RequestModel> BuildSort(RequestSearchRequest r)
    {
        // Map to the Mongo field name via the whitelist; default to CreatedAt if invalid.
        var field = RequestSearchRequest.SortableFields.TryGetValue(r.SortBy ?? string.Empty, out var mapped)
            ? mapped
            : "CreatedAt";

        var sb = Builders<RequestModel>.Sort;
        return r.SortDirection == SortDirection.Asc
            ? sb.Ascending(field).Ascending(x => x.Id)
            : sb.Descending(field).Descending(x => x.Id);
    }

    private static BsonArray GroupBy(string field) =>
    [
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", $"${field}" },
            { "count", new BsonDocument("$sum", 1) },
        }),
    ];

    /// <summary>Enums are stored as Int32; unknown (or null) values are skipped.</summary>
    private static IReadOnlyDictionary<TEnum, long> ToCounts<TEnum>(BsonDocument facetResult, string facet)
        where TEnum : struct, Enum
    {
        var counts = new Dictionary<TEnum, long>();
        if (!facetResult.TryGetValue(facet, out var groups))
        {
            return counts;
        }

        foreach (var group in groups.AsBsonArray.Select(g => g.AsBsonDocument))
        {
            var id = group["_id"];
            if (!id.IsNumeric)
            {
                continue;
            }

            var value = (TEnum)Enum.ToObject(typeof(TEnum), id.ToInt32());
            if (Enum.IsDefined(value))
            {
                counts[value] = group["count"].ToInt64();
            }
        }

        return counts;
    }
}

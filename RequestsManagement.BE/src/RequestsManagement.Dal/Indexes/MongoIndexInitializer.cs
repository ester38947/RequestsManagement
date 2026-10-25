using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;

namespace RequestsManagement.Dal.Indexes;

/// <summary>
/// Creates the indexes that support filtering/search/sorting and history, at startup.
///
/// Every sort index ends with _id: the application sort is always (field, _id) — a tiebreaker that
/// guarantees stable paging — and an index with the same key order returns the results already
/// sorted, without an in-memory SORT. A reverse scan of the same index serves the opposite
/// direction, so one index per sort field is enough.
///
/// Reconcile: if a managed index (ix_/tx_) exists with keys that differ from the definition, or is
/// not in the definition, it is dropped and recreated. That way changing an index definition does
/// not fail with "Index with name ... already exists with different key".
/// </summary>
public sealed class MongoIndexInitializer(AppMongoDbContext context, ILogger<MongoIndexInitializer> logger)
    : IIndexInitializer
{
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Ensuring MongoDB indexes...");

        await EnsureAsync(context.Requests, RequestIndexes(), ct);
        await EnsureAsync(context.StatusAudit, AuditIndexes(), ct);

        logger.LogInformation("MongoDB indexes ensured.");
    }

    private static IReadOnlyList<CreateIndexModel<RequestModel>> RequestIndexes()
    {
        var k = Builders<RequestModel>.IndexKeys;

        return
        [
            // Status/Priority filter (Eq) + CreatedAt sort — the common query on screen.
            Model(k.Ascending(r => r.Status).Ascending(r => r.Priority).Descending(r => r.CreatedAt).Descending(r => r.Id),
                "ix_status_priority_createdAt"),

            // Default: CreatedAt sort; also date ranges.
            Model(k.Descending(r => r.CreatedAt).Descending(r => r.Id), "ix_createdAt"),

            // Sorting by each whitelisted field.
            Model(k.Descending(r => r.UpdatedAt).Descending(r => r.Id), "ix_updatedAt"),
            Model(k.Ascending(r => r.Status).Ascending(r => r.Id), "ix_status"),
            Model(k.Ascending(r => r.Priority).Ascending(r => r.Id), "ix_priority"),
            Model(k.Ascending(r => r.Title).Ascending(r => r.Id), "ix_title"),

            // Filter (Eq) + sort by organization.
            Model(k.Ascending(r => r.OrganizationName).Ascending(r => r.Id), "ix_organizationName"),

            // Filter by assignee.
            Model(k.Ascending(r => r.AssignedTo), "ix_assignedTo"),

            // Text search on Title + OrganizationName.
            Model(k.Text(r => r.Title).Text(r => r.OrganizationName), "tx_title_organizationName"),
        ];
    }

    private static IReadOnlyList<CreateIndexModel<StatusAuditEntry>> AuditIndexes()
    {
        var k = Builders<StatusAuditEntry>.IndexKeys;

        // Fetch history by request, newest to oldest.
        return [Model(k.Ascending(a => a.RequestId).Descending(a => a.ChangedAt), "ix_requestId_changedAt")];
    }

    private async Task EnsureAsync<T>(
        IMongoCollection<T> collection,
        IReadOnlyList<CreateIndexModel<T>> desired,
        CancellationToken ct)
    {
        var desiredKeys = desired.ToDictionary(m => m.Options.Name, m => Render(m.Keys));

        using var cursor = await collection.Indexes.ListAsync(ct);
        var existing = await cursor.ToListAsync(ct);

        foreach (var index in existing)
        {
            var name = index["name"].AsString;
            if (!IsManaged(name))
            {
                continue; // _id_ and indexes we did not create — leave them alone.
            }

            var key = index["key"].AsBsonDocument;
            var keep = desiredKeys.TryGetValue(name, out var wanted) && (IsTextIndex(key) || key.Equals(wanted));
            if (!keep)
            {
                logger.LogWarning(
                    "Dropping outdated index {Index} on {Collection} (key {Key})",
                    name, collection.CollectionNamespace.CollectionName, key);
                await collection.Indexes.DropOneAsync(name, ct);
            }
        }

        await collection.Indexes.CreateManyAsync(desired, ct);
    }

    private static CreateIndexModel<T> Model<T>(IndexKeysDefinition<T> keys, string name) =>
        new(keys, new CreateIndexOptions { Name = name });

    private static BsonDocument Render<T>(IndexKeysDefinition<T> keys) =>
        keys.Render(new RenderArgs<T>(BsonSerializer.LookupSerializer<T>(), BsonSerializer.SerializerRegistry));

    private static bool IsManaged(string name) => name.StartsWith("ix_") || name.StartsWith("tx_");

    // listIndexes returns a text index in an internal form ({_fts, _ftsx}) — compare it by name only.
    private static bool IsTextIndex(BsonDocument key) => key.Contains("_fts");
}

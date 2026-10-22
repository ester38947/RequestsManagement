using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;

namespace RequestsManagement.Dal.Indexes;

/// <summary>
/// יוצר את האינדקסים התומכים בסינון/חיפוש/מיון (Epic 2) ובהיסטוריה (Epic 3), ב-startup.
///
/// כל אינדקס מיון מסתיים ב-_id: המיון באפליקציה הוא תמיד (שדה, _id) — שובר שוויון שמבטיח דפדוף יציב —
/// ואינדקס עם אותו סדר מפתחות נותן את התוצאות ממוינות בלי SORT בזיכרון. סריקה הפוכה של אותו אינדקס
/// משרתת גם את הכיוון ההפוך, ולכן אינדקס אחד לכל שדה מיון.
///
/// Reconcile: אם אינדקס מנוהל (ix_/tx_) קיים עם מפתחות שונים מההגדרה, או שאינו בהגדרה, הוא נמחק ונוצר מחדש.
/// כך שינוי בהגדרת אינדקס לא נכשל על "Index with name ... already exists with different key".
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
            // סינון Status/Priority (Eq) + מיון CreatedAt — השאילתה הנפוצה במסך.
            Model(k.Ascending(r => r.Status).Ascending(r => r.Priority).Descending(r => r.CreatedAt).Descending(r => r.Id),
                "ix_status_priority_createdAt"),

            // ברירת המחדל: מיון CreatedAt; גם טווח תאריכים.
            Model(k.Descending(r => r.CreatedAt).Descending(r => r.Id), "ix_createdAt"),

            // מיון לפי כל שדה ב-whitelist.
            Model(k.Descending(r => r.UpdatedAt).Descending(r => r.Id), "ix_updatedAt"),
            Model(k.Ascending(r => r.Status).Ascending(r => r.Id), "ix_status"),
            Model(k.Ascending(r => r.Priority).Ascending(r => r.Id), "ix_priority"),
            Model(k.Ascending(r => r.Title).Ascending(r => r.Id), "ix_title"),

            // סינון (Eq) + מיון לפי ארגון.
            Model(k.Ascending(r => r.OrganizationName).Ascending(r => r.Id), "ix_organizationName"),

            // סינון לפי משויך.
            Model(k.Ascending(r => r.AssignedTo), "ix_assignedTo"),

            // חיפוש טקסטואלי ב-Title + OrganizationName (Task 2.3).
            Model(k.Text(r => r.Title).Text(r => r.OrganizationName), "tx_title_organizationName"),
        ];
    }

    private static IReadOnlyList<CreateIndexModel<StatusAuditEntry>> AuditIndexes()
    {
        var k = Builders<StatusAuditEntry>.IndexKeys;

        // שליפת היסטוריה לפי פנייה, מהחדש לישן (Task 3.5).
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
                continue; // _id_ ואינדקסים שלא אנחנו יצרנו — לא נוגעים.
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

    // listIndexes מחזיר אינדקס טקסט בצורה פנימית ({_fts, _ftsx}) — משווים אותו לפי שם בלבד.
    private static bool IsTextIndex(BsonDocument key) => key.Contains("_fts");
}

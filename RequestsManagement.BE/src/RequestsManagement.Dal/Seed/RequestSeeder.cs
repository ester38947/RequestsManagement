using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Seed;

/// <summary>
/// Seeds varied demo requests in bulk (InsertMany with batching).
/// The data varies across organizations, statuses, priorities, assignees and a range of creation
/// dates, to allow realistic testing of filtering/search/sorting/aggregations over 100K+ rows.
/// </summary>
public sealed class RequestSeeder(AppMongoDbContext context, ILogger<RequestSeeder> logger) : ISeeder
{
    private const int BatchSize = 5_000;

    private static readonly string[] Organizations =
    [
        "משרד הבריאות", "משרד החינוך", "משרד התחבורה", "רשות המים", "עיריית תל אביב",
        "עיריית ירושלים", "המוסד לביטוח לאומי", "רשות המסים", "משרד הפנים", "משרד המשפטים",
    ];

    private static readonly string[] Assignees =
    [
        "דנה כהן", "יוסי לוי", "מאיה אברהם", "אבי מזרחי", "נועה פרידמן",
        "איתי שפירא", "תמר גולן", "רון ביטון", "שירה דהן", "עומר נחמיאס",
    ];

    private static readonly string[] TitleSubjects =
    [
        "בקשה לאישור", "פנייה בנושא תקלה", "ערעור על החלטה", "בקשת מידע", "תלונה על שירות",
        "בקשה לחידוש רישיון", "דיווח על אי-סדר", "בקשת החזר כספי", "פנייה דחופה", "בקשה לתיאום פגישה",
    ];

    private static readonly RequestStatus[] Statuses =
        [RequestStatus.New, RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.Completed];

    private static readonly RequestPriority[] Priorities =
        [RequestPriority.Low, RequestPriority.Medium, RequestPriority.High];

    public Task<long> CountRequestsAsync(CancellationToken ct = default) =>
        context.Requests.CountDocumentsAsync(FilterDefinition<RequestModel>.Empty, cancellationToken: ct);

    public async Task ResetAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // DeleteMany, not Drop: Drop would also remove the indexes, so later queries would run without them.
        var requests = await context.Requests.DeleteManyAsync(FilterDefinition<RequestModel>.Empty, ct);
        var audit = await context.StatusAudit.DeleteManyAsync(FilterDefinition<StatusAuditEntry>.Empty, ct);

        logger.LogInformation(
            "Seed reset: deleted {Requests} requests and {Audit} audit entries in {Elapsed} ms.",
            requests.DeletedCount, audit.DeletedCount, sw.ElapsedMilliseconds);
    }

    public async Task<long> SeedRequestsAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return 0;
        }

        logger.LogInformation("Seeding {Count} requests (batch size {BatchSize})...", count, BatchSize);
        var sw = Stopwatch.StartNew();

        var rng = new Random();
        var now = DateTime.UtcNow;
        long inserted = 0;

        var buffer = new List<RequestModel>(BatchSize);
        for (var i = 0; i < count; i++)
        {
            buffer.Add(BuildRandomRequest(rng, now));

            if (buffer.Count == BatchSize)
            {
                await InsertBatchAsync(buffer, ct);
                inserted += buffer.Count;
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            await InsertBatchAsync(buffer, ct);
            inserted += buffer.Count;
        }

        sw.Stop();
        logger.LogInformation("Seeded {Inserted} requests in {Elapsed} ms.", inserted, sw.ElapsedMilliseconds);
        return inserted;
    }

    private async Task InsertBatchAsync(List<RequestModel> batch, CancellationToken ct)
    {
        // ordered:false gives higher throughput; there are no unique keys that could collide.
        await context.Requests.InsertManyAsync(
            batch,
            new InsertManyOptions { IsOrdered = false },
            ct);
    }

    private static RequestModel BuildRandomRequest(Random rng, DateTime now)
    {
        // Spread CreatedAt across the last two years (0..730 days back).
        var createdAt = now.AddMinutes(-rng.NextInt64(0, 730L * 24 * 60));
        var status = Statuses[rng.Next(Statuses.Length)];

        // UpdatedAt >= CreatedAt; for non-New requests, simulate a later update.
        var updatedAt = status == RequestStatus.New
            ? createdAt
            : createdAt.AddMinutes(rng.Next(1, 60 * 24 * 30));
        if (updatedAt > now)
        {
            updatedAt = now;
        }

        var org = Organizations[rng.Next(Organizations.Length)];
        var subject = TitleSubjects[rng.Next(TitleSubjects.Length)];

        return new RequestModel
        {
            Title = $"{subject} #{rng.Next(1000, 9999)}",
            OrganizationName = org,
            Status = status,
            Priority = Priorities[rng.Next(Priorities.Length)],
            // Some requests are unassigned (simulates New waiting for assignment).
            AssignedTo = status == RequestStatus.New && rng.Next(2) == 0
                ? null
                : Assignees[rng.Next(Assignees.Length)],
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = 0,
        };
    }
}

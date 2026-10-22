using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Seed;

/// <summary>
/// מזין פניות דמו מגוונות ב-bulk (InsertMany עם batching).
/// הנתונים מגוונים על פני ארגונים, סטטוסים, עדיפויות, משויכים וטווח תאריכי יצירה
/// כדי לאפשר בדיקת סינון/חיפוש/מיון/aggregations ריאליסטית על 100K+.
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

        // DeleteMany ולא Drop: Drop היה מוחק גם את האינדקסים, ואז השאילתות הבאות היו רצות בלעדיהם.
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
        // ordered:false מאפשר תפוקה גבוהה יותר; אין מפתחות ייחודיים שעלולים להתנגש.
        await context.Requests.InsertManyAsync(
            batch,
            new InsertManyOptions { IsOrdered = false },
            ct);
    }

    private static RequestModel BuildRandomRequest(Random rng, DateTime now)
    {
        // פיזור CreatedAt על פני השנתיים האחרונות (0..730 ימים אחורה).
        var createdAt = now.AddMinutes(-rng.NextInt64(0, 730L * 24 * 60));
        var status = Statuses[rng.Next(Statuses.Length)];

        // UpdatedAt >= CreatedAt; לפניות שאינן New נדמה עדכון מאוחר יותר.
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
            // חלק מהפניות לא משויכות (מדמה New ממתין להקצאה).
            AssignedTo = status == RequestStatus.New && rng.Next(2) == 0
                ? null
                : Assignees[rng.Next(Assignees.Length)],
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = 0,
        };
    }
}

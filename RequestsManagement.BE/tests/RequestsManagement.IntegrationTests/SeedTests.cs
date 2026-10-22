using System.Net;
using MongoDB.Driver;
using RequestsManagement.Api.Controllers;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.IntegrationTests.Infrastructure;

namespace RequestsManagement.IntegrationTests;

/// <summary>יצירה חוזרת של נתוני הבדיקה: reset=true מוחק את הקיים (כולל היסטוריה) ויוצר בדיוק count פניות.</summary>
public sealed class SeedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SeedWithReset_RecreatesExactlyCount_AndClearsHistory()
    {
        var existing = await factory.InsertAsync(RequestStatus.New);
        await factory.Db.StatusAudit.InsertOneAsync(new StatusAuditEntry
        {
            RequestId = existing.Id,
            PreviousStatus = RequestStatus.New,
            NewStatus = RequestStatus.Waiting,
            ChangedAt = DateTime.UtcNow,
        });

        // בלי reset — מוסיף לקיים.
        var appended = await Seed("count=30");
        Assert.Equal(31, appended.TotalInCollection);

        // עם reset — מתחיל מאפס: בדיוק count פניות, ובלי היסטוריה ישנה.
        var recreated = await Seed("count=20&reset=true");
        Assert.True(recreated.Reset);
        Assert.Equal(20, recreated.Inserted);
        Assert.Equal(20, recreated.TotalInCollection);
        Assert.Equal(0, await factory.Db.StatusAudit.CountDocumentsAsync(FilterDefinition<StatusAuditEntry>.Empty));

        // האינדקסים נשמרו (DeleteMany ולא Drop) — חיפוש טקסט, שדורש אינדקס טקסט, עדיין עובד.
        var search = await _client.GetAsync("/api/requests?search=%D7%93%D7%97%D7%95%D7%A4%D7%94");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
    }

    private async Task<SeedResultDto> Seed(string query)
    {
        var response = await _client.PostAsync($"/api/seed?{query}", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<SeedResultDto>();
    }
}

using System.Net;
using MongoDB.Driver;
using RequestsManagement.Api.Controllers;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.IntegrationTests.Infrastructure;

namespace RequestsManagement.IntegrationTests;

/// <summary>Repeatable recreation of the test data: reset=true deletes the existing data (including history) and creates exactly count requests.</summary>
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

        // Without reset — adds to the existing data.
        var appended = await Seed("count=30");
        Assert.Equal(31, appended.TotalInCollection);

        // With reset — starts from scratch: exactly count requests, and no old history.
        var recreated = await Seed("count=20&reset=true");
        Assert.True(recreated.Reset);
        Assert.Equal(20, recreated.Inserted);
        Assert.Equal(20, recreated.TotalInCollection);
        Assert.Equal(0, await factory.Db.StatusAudit.CountDocumentsAsync(FilterDefinition<StatusAuditEntry>.Empty));

        // The indexes were preserved (DeleteMany, not Drop) — text search, which needs a text index, still works.
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

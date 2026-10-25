using System.Net;
using MongoDB.Driver;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;
using RequestsManagement.IntegrationTests.Infrastructure;

namespace RequestsManagement.IntegrationTests;

/// <summary>Status update, optimistic concurrency, audit and bulk — end to end against Mongo (including transactions).</summary>
public sealed class StatusUpdateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ParallelUpdatesWithSameVersion_ExactlyOneSucceeds_NoLostUpdate()
    {
        var request = await factory.InsertAsync(RequestStatus.New);

        // 8 clients that saw the same version (0) try to update in parallel.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _client.PatchJsonAsync($"/api/requests/{request.Id}/status",
                new { newStatus = "InProgress", expectedVersion = 0, changedBy = $"user{i}" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var stored = await factory.Db.Requests.Find(r => r.Id == request.Id).SingleAsync();
        Assert.Equal(1, stored.Version);
        Assert.Equal(RequestStatus.InProgress, stored.Status);
        Assert.True(stored.UpdatedAt > request.UpdatedAt);

        var audit = await factory.Db.StatusAudit.Find(a => a.RequestId == request.Id).ToListAsync();
        Assert.Single(audit);

        var conflict = await responses.First(r => r.StatusCode == HttpStatusCode.Conflict).ReadJsonAsync();
        Assert.Equal(1, conflict.GetProperty("currentVersion").GetInt32());
        Assert.Equal("InProgress", conflict.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task ValidUpdate_ReturnsNewVersion_AndAppearsInHistory()
    {
        var request = await factory.InsertAsync(RequestStatus.New);

        var first = await _client.PatchJsonAsync($"/api/requests/{request.Id}/status",
            new { newStatus = "Waiting", expectedVersion = 0, changedBy = "dana" });
        var updated = await first.ReadDataAsync<RequestListItemDto>();
        Assert.Equal(1, updated.Version);

        var second = await _client.PatchJsonAsync($"/api/requests/{request.Id}/status",
            new { newStatus = "Completed", expectedVersion = updated.Version, changedBy = "avi" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var history = await (await _client.GetAsync($"/api/requests/{request.Id}/history"))
            .ReadDataAsync<List<RequestStatusHistoryItemDto>>();

        Assert.Equal(2, history.Count);
        Assert.Equal((RequestStatus.Waiting, RequestStatus.Completed, "avi"),
            (history[0].PreviousStatus, history[0].NewStatus, history[0].ChangedBy)); // newest to oldest
        Assert.Equal((RequestStatus.New, RequestStatus.Waiting, "dana"),
            (history[1].PreviousStatus, history[1].NewStatus, history[1].ChangedBy));
    }

    [Fact]
    public async Task InvalidTransition_Returns400_AndLeavesRequestUnchanged()
    {
        var request = await factory.InsertAsync(RequestStatus.Completed);

        var response = await _client.PatchJsonAsync($"/api/requests/{request.Id}/status",
            new { newStatus = "InProgress", expectedVersion = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await factory.Db.Requests.Find(r => r.Id == request.Id).SingleAsync();
        Assert.Equal((RequestStatus.Completed, 0), (stored.Status, stored.Version));
    }

    [Fact]
    public async Task UnknownRequest_Returns404()
    {
        var response = await _client.PatchJsonAsync("/api/requests/000000000000000000000000/status",
            new { newStatus = "Waiting", expectedVersion = 0 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownStatusValue_Returns400_InUnifiedFormat_WithAllowedValues()
    {
        var request = await factory.InsertAsync(RequestStatus.New);

        var response = await _client.PatchAsync($"/api/requests/{request.Id}/status",
            Json("""{ "newStatus": "Foo", "expectedVersion": 0 }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("שגיאות ולידציה", body.GetProperty("title").GetString());

        var errors = body.GetProperty("errors");
        var message = Assert.Single(errors.GetProperty("NewStatus").EnumerateArray()).GetString();
        Assert.Contains("InProgress", message);
        // Without the confusing default error on the action parameter ("The request field is required").
        Assert.False(errors.TryGetProperty("request", out _));

        var stored = await factory.Db.Requests.Find(r => r.Id == request.Id).SingleAsync();
        Assert.Equal(0, stored.Version);
    }

    [Fact]
    public async Task MalformedJsonBody_Returns400_WithBodyError()
    {
        var request = await factory.InsertAsync(RequestStatus.New);

        var response = await _client.PatchAsync($"/api/requests/{request.Id}/status", Json("""{ "newStatus": """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("body", out _));
        Assert.False(errors.TryGetProperty("request", out _));
    }

    [Fact]
    public async Task BulkUpdate_PartialSuccess_ReportsEachItem()
    {
        var ok = await factory.InsertAsync(RequestStatus.New);
        var stale = await factory.InsertAsync(RequestStatus.New, version: 2);
        var completed = await factory.InsertAsync(RequestStatus.Completed);
        const string missing = "000000000000000000000000";

        var response = await _client.PatchJsonAsync("/api/requests/bulk-status", new
        {
            newStatus = "Waiting",
            items = new object[]
            {
                new { id = ok.Id, expectedVersion = 0 },
                new { id = stale.Id, expectedVersion = 1 },
                new { id = completed.Id, expectedVersion = 0 },
                new { id = missing, expectedVersion = 0 },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadDataAsync<BulkUpdateStatusResultDto>();
        Assert.Equal((4, 1, 3), (result.Total, result.Succeeded, result.Failed));
        Assert.Equal(BulkItemOutcome.Updated, Outcome(result, ok.Id));
        Assert.Equal(BulkItemOutcome.Conflict, Outcome(result, stale.Id));
        Assert.Equal(BulkItemOutcome.InvalidTransition, Outcome(result, completed.Id));
        Assert.Equal(BulkItemOutcome.NotFound, Outcome(result, missing));

        // Only the updated request got an audit record; the others were unchanged.
        var audited = await factory.Db.StatusAudit
            .Find(Builders<StatusAuditEntry>.Filter.In(a => a.RequestId, new[] { ok.Id, stale.Id, completed.Id }))
            .ToListAsync();
        Assert.Equal(ok.Id, Assert.Single(audited).RequestId);
        Assert.Equal(2, (await factory.Db.Requests.Find(r => r.Id == stale.Id).SingleAsync()).Version);
    }

    private static BulkItemOutcome Outcome(BulkUpdateStatusResultDto result, string id) =>
        result.Items.Single(i => i.Id == id).Outcome;

    private static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}

using System.Net;
using MongoDB.Driver;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;
using RequestsManagement.IntegrationTests.Infrastructure;

namespace RequestsManagement.IntegrationTests;

/// <summary>Query: combined filtering, server-side paging and sorting, text search and validation — end to end against Mongo.</summary>
public sealed class RequestsQueryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly DateTime BaseTime = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly HttpClient _client = factory.CreateClient();
    private readonly List<string> _matchingIdsNewestFirst = [];

    public async Task InitializeAsync()
    {
        await factory.Db.Requests.DeleteManyAsync(FilterDefinition<Dal.Models.RequestModel>.Empty);

        // 5 matches (Completed + High) with known dates, plus noise that fails one of the criteria.
        for (var i = 0; i < 5; i++)
        {
            var m = await factory.InsertAsync(RequestStatus.Completed, RequestPriority.High,
                title: $"Match {i}", createdAt: BaseTime.AddHours(i));
            _matchingIdsNewestFirst.Insert(0, m.Id);
        }

        await factory.InsertAsync(RequestStatus.Completed, RequestPriority.Low, title: "Other priority");
        await factory.InsertAsync(RequestStatus.New, RequestPriority.High, title: "Other status");
        await factory.InsertAsync(RequestStatus.Waiting, RequestPriority.Medium, title: "Urgent approval needed");
        await factory.InsertAsync(RequestStatus.New, RequestPriority.Medium, title: "Urgent refund", organization: "Org B");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CombinedFilter_ReturnsOnlyMatches_WithServerSidePagingAndSorting()
    {
        var page1 = await GetPage("status=Completed&priority=High&pageSize=2&page=1&sortBy=createdAt&sortDirection=Desc");
        var page3 = await GetPage("status=Completed&priority=High&pageSize=2&page=3&sortBy=createdAt&sortDirection=Desc");

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.All(page1.Items, i =>
        {
            Assert.Equal(RequestStatus.Completed, i.Status);
            Assert.Equal(RequestPriority.High, i.Priority);
        });
        Assert.Equal(_matchingIdsNewestFirst.Take(2), page1.Items.Select(i => i.Id));
        Assert.Equal(_matchingIdsNewestFirst.Skip(4), page3.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Paging_IsStable_AcrossPages_WhenSortingByLowCardinalityField()
    {
        // Sort by priority (only 3 values, many repeats): all pages together must cover each request exactly once.
        var ids = new List<string>();
        for (var page = 1; page <= 5; page++)
        {
            ids.AddRange((await GetPage($"sortBy=priority&sortDirection=Asc&pageSize=2&page={page}")).Items.Select(i => i.Id));
        }

        Assert.Equal(9, ids.Count);
        Assert.Equal(9, ids.Distinct().Count());
    }

    [Fact]
    public async Task TextSearch_MatchesTitleWords()
    {
        var result = await GetPage("search=urgent");

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Contains("Urgent", i.Title));
    }

    [Fact]
    public async Task InvalidQuery_Returns400_WithPerFieldValidationErrors()
    {
        var response = await _client.GetAsync("/api/requests?pageSize=500&sortBy=nope");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("PageSize", out _));
        Assert.True(errors.TryGetProperty("SortBy", out _));
    }

    [Fact]
    public async Task UnparsableQueryValues_Return400_InSameFormatAsValidation()
    {
        // The values fail at model binding (before FluentValidation) — the response is still in the same format and in Hebrew.
        var response = await _client.GetAsync("/api/requests?status=Foo&page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("שגיאות ולידציה", body.GetProperty("title").GetString());

        var errors = body.GetProperty("errors");
        Assert.Contains("Completed", Assert.Single(errors.GetProperty("Status").EnumerateArray()).GetString());
        Assert.True(errors.TryGetProperty("Page", out _));
    }

    [Fact]
    public async Task Summary_CountsByStatusAndPriority()
    {
        var response = await _client.GetAsync("/api/requests/summary");
        var summary = await response.ReadDataAsync<RequestsSummaryDto>();

        Assert.Equal(9, summary.Total);
        Assert.Equal(6, summary.ByStatus.Single(s => s.Status == RequestStatus.Completed).Count);
        Assert.Equal(6, summary.ByPriority.Single(p => p.Priority == RequestPriority.High).Count);
        Assert.Equal(summary.Total, summary.ByStatus.Sum(s => s.Count));
    }

    private async Task<PagedResultDto<RequestListItemDto>> GetPage(string query)
    {
        var response = await _client.GetAsync($"/api/requests?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<PagedResultDto<RequestListItemDto>>();
    }
}

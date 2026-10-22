using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.UnitTests.Validators;

public sealed class RequestSearchRequestValidatorTests
{
    private readonly RequestSearchRequestValidator _validator = new();

    [Fact]
    public void Defaults_AreValid()
    {
        Assert.True(_validator.Validate(new RequestSearchRequest()).IsValid);
    }

    [Theory]
    [InlineData(0, 25, "createdAt", nameof(RequestSearchRequest.Page))]
    [InlineData(1, 0, "createdAt", nameof(RequestSearchRequest.PageSize))]
    [InlineData(1, 101, "createdAt", nameof(RequestSearchRequest.PageSize))]
    [InlineData(1, 25, "assignedTo", nameof(RequestSearchRequest.SortBy))]
    [InlineData(1, 25, "$where", nameof(RequestSearchRequest.SortBy))]
    public void InvalidPagingOrSort_IsRejected(int page, int pageSize, string sortBy, string expectedProperty)
    {
        var result = _validator.Validate(new RequestSearchRequest { Page = page, PageSize = pageSize, SortBy = sortBy });

        Assert.Contains(result.Errors, e => e.PropertyName == expectedProperty);
    }

    [Fact]
    public void InvertedDateRange_IsRejected()
    {
        var result = _validator.Validate(new RequestSearchRequest
        {
            CreatedFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedTo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SearchLongerThanLimit_IsRejected()
    {
        var result = _validator.Validate(new RequestSearchRequest { Search = new string('x', 201) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestSearchRequest.Search));
    }
}

public sealed class BulkUpdateStatusRequestValidatorTests
{
    private readonly BulkUpdateStatusRequestValidator _validator = new();

    [Fact]
    public void MoreThan100Items_IsRejected()
    {
        var items = Enumerable.Range(1, 101)
            .Select(i => new BulkUpdateStatusItem { Id = i.ToString("x24"), ExpectedVersion = 0 })
            .ToList();

        var result = _validator.Validate(new BulkUpdateStatusRequest { NewStatus = RequestStatus.Waiting, Items = items });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BulkUpdateStatusRequest.Items));
    }

    [Fact]
    public void EmptyList_IsRejected()
    {
        var result = _validator.Validate(new BulkUpdateStatusRequest { NewStatus = RequestStatus.Waiting, Items = [] });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BulkUpdateStatusRequest.Items));
    }

    [Fact]
    public void DuplicateIdsDifferingOnlyInCase_AreRejected()
    {
        const string id = "6ada1441213c6aaa945a14b8";
        var result = _validator.Validate(new BulkUpdateStatusRequest
        {
            NewStatus = RequestStatus.Waiting,
            Items = [new() { Id = id, ExpectedVersion = 0 }, new() { Id = id.ToUpperInvariant(), ExpectedVersion = 0 }],
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UnreachableTargetStatus_IsRejected()
    {
        var result = _validator.Validate(new BulkUpdateStatusRequest
        {
            NewStatus = RequestStatus.New,
            Items = [new() { Id = "6ada1441213c6aaa945a14b8", ExpectedVersion = 0 }],
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BulkUpdateStatusRequest.NewStatus));
    }

    [Fact]
    public void ReportsErrorsForEveryInvalidItem()
    {
        var result = _validator.Validate(new BulkUpdateStatusRequest
        {
            NewStatus = RequestStatus.Waiting,
            Items = [new() { Id = "bad", ExpectedVersion = 0 }, new() { Id = "6ada1441213c6aaa945a14b8" }],
        });

        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].Id");
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[1].ExpectedVersion");
    }
}

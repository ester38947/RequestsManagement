using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.Bl.Exceptions;
using RequestsManagement.Bl.Services;
using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;
using RequestsManagement.UnitTests.Fakes;

namespace RequestsManagement.UnitTests.Services;

public sealed class RequestServiceTests
{
    private readonly FakeRequestRepository _repo = new();
    private readonly CountingSummaryCache _cache = new();
    private readonly RequestService _service;

    public RequestServiceTests()
    {
        _service = new RequestService(
            _repo,
            new FakeStatusAuditRepository(),
            new RequestSearchRequestValidator(),
            new UpdateRequestStatusRequestValidator(),
            new BulkUpdateStatusRequestValidator(),
            _cache,
            NullLogger<RequestService>.Instance);
    }

    // ---------- Concurrency ----------

    [Fact]
    public async Task UpdateStatus_WithStaleVersion_ThrowsConflict_AndDoesNotWrite()
    {
        var request = _repo.Add(RequestStatus.InProgress, version: 3);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            _service.UpdateStatusAsync(request.Id, Body(RequestStatus.Waiting, expectedVersion: 2)));

        Assert.Equal(3, ex.CurrentVersion);
        Assert.Equal(RequestStatus.InProgress, ex.CurrentStatus);
        Assert.Equal(0, _repo.UpdateCalls);
        Assert.Empty(_repo.Audit);
        Assert.Equal(0, _cache.Invalidations);
    }

    [Fact]
    public async Task UpdateStatus_WhenConcurrentWriteWinsAtDb_ThrowsConflictWithLatestState()
    {
        // The pre-check passes (version matches), but another update is written before ours — the DB condition fails.
        var request = _repo.Add(RequestStatus.New, version: 0);
        _repo.SimulateConcurrentWriteBeforeUpdate = true;

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            _service.UpdateStatusAsync(request.Id, Body(RequestStatus.InProgress, expectedVersion: 0)));

        Assert.Equal(1, ex.CurrentVersion);
        Assert.Equal(RequestStatus.Waiting, ex.CurrentStatus);
        Assert.Empty(_repo.Audit); // Our update was not written — no Lost Update.
        Assert.Equal(0, _cache.Invalidations);
    }

    [Fact]
    public async Task UpdateStatus_WithCurrentVersion_UpdatesIncrementsVersionAndInvalidatesSummary()
    {
        var request = _repo.Add(RequestStatus.New, version: 0);

        var updated = await _service.UpdateStatusAsync(
            request.Id, Body(RequestStatus.InProgress, expectedVersion: 0, changedBy: "dana"));

        Assert.Equal(RequestStatus.InProgress, updated.Status);
        Assert.Equal(1, updated.Version);
        var audit = Assert.Single(_repo.Audit);
        Assert.Equal(RequestStatus.New, audit.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, audit.NewStatus);
        Assert.Equal("dana", audit.ChangedBy);
        Assert.Equal(1, _cache.Invalidations);
    }

    // ---------- Invalid input / domain rules ----------

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ThrowsWithAllowedTargets()
    {
        var request = _repo.Add(RequestStatus.Completed, version: 0);

        var ex = await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _service.UpdateStatusAsync(request.Id, Body(RequestStatus.InProgress, expectedVersion: 0)));

        Assert.Equal(RequestStatus.Completed, ex.From);
        Assert.Empty(ex.Allowed);
        Assert.Equal(0, _repo.UpdateCalls);
    }

    [Fact]
    public async Task UpdateStatus_UnknownRequest_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateStatusAsync("000000000000000000000000", Body(RequestStatus.Waiting, expectedVersion: 0)));
    }

    [Fact]
    public async Task UpdateStatus_MissingExpectedVersion_ThrowsValidation()
    {
        // Without expectedVersion, a default of 0 would bypass the concurrency check — it must be a 400.
        var request = _repo.Add(RequestStatus.New, version: 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateStatusAsync(request.Id, new UpdateRequestStatusRequest { NewStatus = RequestStatus.Waiting }));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(UpdateRequestStatusRequest.ExpectedVersion));
        Assert.Equal(0, _repo.UpdateCalls);
    }

    [Fact]
    public async Task UpdateStatus_MalformedId_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateStatusAsync("not-an-id", Body(RequestStatus.Waiting, expectedVersion: 0)));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Id");
    }

    [Fact]
    public async Task Search_InvalidPaging_ThrowsValidation_WithoutQueryingDb()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SearchAsync(new RequestSearchRequest { Page = 0, PageSize = 500 }));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RequestSearchRequest.Page));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RequestSearchRequest.PageSize));
        Assert.Equal(0, _repo.SearchCalls);
    }

    // ---------- Bulk ----------

    [Fact]
    public async Task BulkUpdate_ReportsEachItem_AndInvalidatesSummaryOnce()
    {
        var ok1 = _repo.Add(RequestStatus.New, version: 0);
        var ok2 = _repo.Add(RequestStatus.Waiting, version: 2);
        var stale = _repo.Add(RequestStatus.New, version: 4);
        var completed = _repo.Add(RequestStatus.Completed, version: 1);
        const string missing = "000000000000000000000000";

        var result = await _service.BulkUpdateStatusAsync(new BulkUpdateStatusRequest
        {
            NewStatus = RequestStatus.InProgress,
            Items =
            [
                new() { Id = ok1.Id, ExpectedVersion = 0 },
                new() { Id = ok2.Id.ToUpperInvariant(), ExpectedVersion = 2 },
                new() { Id = stale.Id, ExpectedVersion = 3 },
                new() { Id = completed.Id, ExpectedVersion = 1 },
                new() { Id = missing, ExpectedVersion = 0 },
            ],
        });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(3, result.Failed);
        Assert.Equal(BulkItemOutcome.Updated, Outcome(result, ok1.Id));
        Assert.Equal(BulkItemOutcome.Updated, Outcome(result, ok2.Id));
        Assert.Equal(BulkItemOutcome.Conflict, Outcome(result, stale.Id));
        Assert.Equal(BulkItemOutcome.InvalidTransition, Outcome(result, completed.Id));
        Assert.Equal(BulkItemOutcome.NotFound, Outcome(result, missing));

        Assert.Equal(1, _repo.Get(ok1.Id).Version);
        Assert.Equal(4, _repo.Get(stale.Id).Version); // The request with the stale version was left untouched.
        Assert.Equal(2, _repo.Audit.Count);
        Assert.Equal(1, _cache.Invalidations);
    }

    [Fact]
    public async Task BulkUpdate_WhenNothingUpdated_DoesNotInvalidateSummary()
    {
        var stale = _repo.Add(RequestStatus.New, version: 1);

        var result = await _service.BulkUpdateStatusAsync(new BulkUpdateStatusRequest
        {
            NewStatus = RequestStatus.Waiting,
            Items = [new() { Id = stale.Id, ExpectedVersion = 0 }],
        });

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, _cache.Invalidations);
    }

    private static UpdateRequestStatusRequest Body(RequestStatus status, int expectedVersion, string? changedBy = null) =>
        new() { NewStatus = status, ExpectedVersion = expectedVersion, ChangedBy = changedBy };

    private static BulkItemOutcome Outcome(BulkUpdateStatusResultDto result, string id) =>
        result.Items.Single(i => i.Id == id).Outcome;
}

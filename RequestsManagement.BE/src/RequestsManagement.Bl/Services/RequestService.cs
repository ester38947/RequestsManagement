using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using RequestsManagement.Bl.Caching;
using RequestsManagement.Bl.Exceptions;
using RequestsManagement.Bl.Interfaces;
using RequestsManagement.Bl.Validators;
using RequestsManagement.Dal.Interfaces;
using RequestsManagement.Dal.Mapping;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Services;

/// <summary>
/// Request logic: runs validation, orchestrates the repositories, and maps to DTOs (never a DB entity).
/// </summary>
public sealed class RequestService(
    IRequestRepository repository,
    IStatusAuditRepository auditRepository,
    IValidator<RequestSearchRequest> searchValidator,
    IValidator<UpdateRequestStatusRequest> updateStatusValidator,
    IValidator<BulkUpdateStatusRequest> bulkUpdateValidator,
    IRequestsSummaryCache summaryCache,
    ILogger<RequestService> logger) : IRequestService
{
    private const string DefaultActor = "system";

    public async Task<PagedResultDto<RequestListItemDto>> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default)
    {
        await searchValidator.ValidateAndThrowAsync(request, ct);

        logger.LogInformation(
            "Searching requests. Page={Page} PageSize={PageSize} Status={Status} Priority={Priority} Search={HasSearch}",
            request.Page, request.PageSize, request.Status, request.Priority, !string.IsNullOrWhiteSpace(request.Search));

        var (items, total) = await repository.SearchAsync(request, ct);
        var dtos = RequestMapper.ToListItemDtos(items);

        return PagedResultDto<RequestListItemDto>.Create(dtos, total, request.Page, request.PageSize);
    }

    /// <summary>Aggregations summary via the cache.</summary>
    public Task<RequestsSummaryDto> GetSummaryAsync(CancellationToken ct = default) =>
        summaryCache.GetOrCreateAsync(ComputeSummaryAsync, ct);

    private async Task<RequestsSummaryDto> ComputeSummaryAsync(CancellationToken ct)
    {
        logger.LogInformation("Computing requests summary (aggregations).");

        var counts = await repository.GetCountsAsync(ct);

        // All enum values are returned (even with 0), ordered by the enum order.
        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(s => new StatusCountDto(s, counts.ByStatus.GetValueOrDefault(s)))
            .ToList();

        var byPriority = Enum.GetValues<RequestPriority>()
            .Select(p => new PriorityCountDto(p, counts.ByPriority.GetValueOrDefault(p)))
            .ToList();

        // The total comes from the same call as the breakdown — always equals the sum by status.
        var total = byStatus.Sum(s => s.Count);

        return new RequestsSummaryDto(total, byStatus, byPriority);
    }

    public async Task<RequestListItemDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        EnsureValidId(id);

        var model = await repository.GetByIdAsync(id, ct);
        return model is null ? null : RequestMapper.ToListItemDto(model);
    }

    /// <summary>
    /// Single update: runs the shared core (<see cref="ApplyStatusChangeAsync"/>) and converts the result
    /// into a meaningful exception — the global handler maps it to 404 / 409 / 400.
    /// </summary>
    public async Task<RequestListItemDto> UpdateStatusAsync(
        string id,
        UpdateRequestStatusRequest request,
        CancellationToken ct = default)
    {
        EnsureValidId(id);
        await updateStatusValidator.ValidateAndThrowAsync(request, ct);

        var newStatus = request.NewStatus!.Value;
        var current = await repository.GetByIdAsync(id, ct);

        var result = await ApplyStatusChangeAsync(
            id, current, request.ExpectedVersion!.Value, newStatus, ResolveActor(request.ChangedBy), ct);

        switch (result.Outcome)
        {
            case BulkItemOutcome.NotFound:
                throw new KeyNotFoundException(ValidationMessages.RequestNotFound);

            case BulkItemOutcome.Conflict:
                throw new ConcurrencyConflictException(id, result.State!.Version, result.State.Status);

            case BulkItemOutcome.InvalidTransition:
                throw new InvalidStatusTransitionException(result.State!.Status, newStatus);
        }

        // The counts changed — invalidate the stored summary. Only after a successful commit.
        summaryCache.Invalidate();

        logger.LogInformation(
            "Request {RequestId} status changed {From} -> {To} (version {Version})",
            id, current!.Status, newStatus, result.State!.Version);

        return RequestMapper.ToListItemDto(result.State);
    }

    /// <summary>
    /// Bulk update (partial success): each request goes through the same core as a single update, in its
    /// own transaction, and the result goes into the report instead of throwing — so the failure of one
    /// request does not stop the others.
    /// </summary>
    public async Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(
        BulkUpdateStatusRequest request,
        CancellationToken ct = default)
    {
        await bulkUpdateValidator.ValidateAndThrowAsync(request, ct);

        var newStatus = request.NewStatus!.Value;
        var actor = ResolveActor(request.ChangedBy);

        // Normalize ids to their canonical form (lowercase hex) so matching against the DB does not fail on case.
        var items = request.Items!
            .Select(i => (Id: ObjectId.Parse(i.Id!.Trim()).ToString(), ExpectedVersion: i.ExpectedVersion!.Value))
            .ToList();

        logger.LogInformation(
            "Bulk status update requested: {Count} requests -> {NewStatus} by {Actor}",
            items.Count, newStatus, actor);

        // Current state of all requests in a single query.
        var currentById = (await repository.GetByIdsAsync(items.Select(i => i.Id).ToList(), ct))
            .ToDictionary(m => m.Id);

        var results = new List<BulkStatusItemResultDto>(items.Count);
        try
        {
            foreach (var (id, expectedVersion) in items)
            {
                // Cancellation stops processing; already-updated requests stay updated (each in its own transaction).
                ct.ThrowIfCancellationRequested();

                var result = await ApplyStatusChangeAsync(
                    id, currentById.GetValueOrDefault(id), expectedVersion, newStatus, actor, ct);
                results.Add(ToBulkItemResult(id, newStatus, result));
            }
        }
        finally
        {
            // Invalidate once for the whole batch, and only if something was actually updated — even if processing was interrupted midway.
            if (results.Any(r => r.Outcome == BulkItemOutcome.Updated))
            {
                summaryCache.Invalidate();
            }
        }

        var succeeded = results.Count(r => r.Outcome == BulkItemOutcome.Updated);
        var failed = results.Count - succeeded;

        if (failed > 0)
        {
            logger.LogWarning(
                "Bulk status update finished with failures: {Succeeded} updated, {NotFound} not found, {Conflicts} conflicts, {Invalid} invalid transitions",
                succeeded,
                results.Count(r => r.Outcome == BulkItemOutcome.NotFound),
                results.Count(r => r.Outcome == BulkItemOutcome.Conflict),
                results.Count(r => r.Outcome == BulkItemOutcome.InvalidTransition));
        }
        else
        {
            logger.LogInformation("Bulk status update finished: all {Succeeded} updated", succeeded);
        }

        return new BulkUpdateStatusResultDto(results.Count, succeeded, failed, results);
    }

    public async Task<IReadOnlyList<RequestStatusHistoryItemDto>?> GetHistoryAsync(
        string id,
        CancellationToken ct = default)
    {
        EnsureValidId(id);

        if (await repository.GetByIdAsync(id, ct) is null)
        {
            return null;
        }

        var entries = await auditRepository.GetByRequestIdAsync(id, ct);
        return StatusAuditMapper.ToHistoryItemDtos(entries);
    }

    /// <summary>
    /// The shared core for single and bulk updates. Order of checks:
    ///   1. The request exists                         -> otherwise NotFound
    ///   2. The version the client saw is the current  -> otherwise Conflict (pre-check — saves a transaction)
    ///   3. The transition is valid per the state machine -> otherwise InvalidTransition
    ///   4. Version-conditioned update + audit in a transaction. If the DB condition did not hold, someone
    ///      updated between the read and the write -> Conflict with the current state. This is the real
    ///      enforcement that prevents a Lost Update.
    /// State: on success, the updated document; on failure, the current state in the DB (null if absent).
    /// </summary>
    private async Task<StatusChangeResult> ApplyStatusChangeAsync(
        string id,
        RequestModel? current,
        int expectedVersion,
        RequestStatus newStatus,
        string actor,
        CancellationToken ct)
    {
        if (current is null)
        {
            return new StatusChangeResult(BulkItemOutcome.NotFound, null);
        }

        if (current.Version != expectedVersion)
        {
            logger.LogWarning(
                "Concurrency conflict on {RequestId}: expected version {ExpectedVersion}, current {CurrentVersion}",
                id, expectedVersion, current.Version);
            return new StatusChangeResult(BulkItemOutcome.Conflict, current);
        }

        if (!RequestStatusTransitions.IsValid(current.Status, newStatus))
        {
            logger.LogWarning(
                "Invalid status transition on {RequestId}: {From} -> {To}",
                id, current.Status, newStatus);
            return new StatusChangeResult(BulkItemOutcome.InvalidTransition, current);
        }

        var updated = await repository.UpdateStatusWithAuditAsync(
            id, expectedVersion, current.Status, newStatus, actor, ct);

        if (updated is not null)
        {
            return new StatusChangeResult(BulkItemOutcome.Updated, updated);
        }

        var latest = await repository.GetByIdAsync(id, ct);
        if (latest is null)
        {
            return new StatusChangeResult(BulkItemOutcome.NotFound, null);
        }

        logger.LogWarning(
            "Concurrency conflict on {RequestId} detected at write: expected version {ExpectedVersion}, current {CurrentVersion}",
            id, expectedVersion, latest.Version);
        return new StatusChangeResult(BulkItemOutcome.Conflict, latest);
    }

    private static BulkStatusItemResultDto ToBulkItemResult(string id, RequestStatus newStatus, StatusChangeResult result) =>
        result.Outcome switch
        {
            BulkItemOutcome.Updated =>
                new(id, result.Outcome, result.State!.Version, result.State.Status, null),
            BulkItemOutcome.Conflict =>
                new(id, result.Outcome, result.State!.Version, result.State.Status, ValidationMessages.ConcurrencyConflict),
            BulkItemOutcome.InvalidTransition =>
                new(id, result.Outcome, result.State!.Version, result.State.Status,
                    string.Format(ValidationMessages.InvalidTransition, result.State.Status, newStatus)),
            _ =>
                new(id, BulkItemOutcome.NotFound, null, null, ValidationMessages.RequestNotFound),
        };

    private static string ResolveActor(string? changedBy) =>
        string.IsNullOrWhiteSpace(changedBy) ? DefaultActor : changedBy.Trim();

    /// <summary>The id must be a valid ObjectId — otherwise 400 (not a serialization exception -> 500).</summary>
    private static void EnsureValidId(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            throw new ValidationException([new ValidationFailure("Id", ValidationMessages.InvalidId)]);
        }
    }

    /// <summary>The core result: what happened, and the relevant request state (updated on success, current on failure).</summary>
    private sealed record StatusChangeResult(BulkItemOutcome Outcome, RequestModel? State);
}

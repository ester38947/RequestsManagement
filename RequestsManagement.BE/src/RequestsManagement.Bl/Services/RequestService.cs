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
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Services;

/// <summary>
/// לוגיקת פניות: מריץ ולידציה, מתזמר את ה-repositories, וממפה ל-DTOs (אף פעם לא ישות DB).
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

    /// <summary>סיכום aggregations דרך המטמון (Task 5.1).</summary>
    public Task<RequestsSummaryDto> GetSummaryAsync(CancellationToken ct = default) =>
        summaryCache.GetOrCreateAsync(ComputeSummaryAsync, ct);

    private async Task<RequestsSummaryDto> ComputeSummaryAsync(CancellationToken ct)
    {
        logger.LogInformation("Computing requests summary (aggregations).");

        var counts = await repository.GetCountsAsync(ct);

        // כל ערכי ה-enum מוחזרים (גם עם 0), ממוינים לפי סדר ה-enum.
        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(s => new StatusCountDto(s, counts.ByStatus.GetValueOrDefault(s)))
            .ToList();

        var byPriority = Enum.GetValues<RequestPriority>()
            .Select(p => new PriorityCountDto(p, counts.ByPriority.GetValueOrDefault(p)))
            .ToList();

        // הסך מאותה קריאה כמו הפילוח — תמיד שווה לסכום לפי סטטוס.
        var total = byStatus.Sum(s => s.Count);

        return new RequestsSummaryDto(total, byStatus, byPriority);
    }

    public async Task<RequestListItemDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        EnsureValidId(id);

        var model = await repository.GetByIdAsync(id, ct);
        return model is null ? null : RequestMapper.ToListItemDto(model);
    }

    public async Task<RequestListItemDto> UpdateStatusAsync(
        string id,
        UpdateRequestStatusRequest request,
        CancellationToken ct = default)
    {
        EnsureValidId(id);
        await updateStatusValidator.ValidateAndThrowAsync(request, ct);

        var newStatus = request.NewStatus!.Value;
        var expectedVersion = request.ExpectedVersion!.Value;
        var actor = string.IsNullOrWhiteSpace(request.ChangedBy) ? DefaultActor : request.ChangedBy.Trim();

        // 404 — הפנייה לא קיימת.
        var current = await repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException(ValidationMessages.RequestNotFound);

        // 409 — הלקוח עובד על גרסה מיושנת (בדיקה מוקדמת; האכיפה האמיתית היא בתנאי ה-update ב-DB).
        if (current.Version != expectedVersion)
        {
            logger.LogWarning(
                "Concurrency conflict on {RequestId}: expected version {ExpectedVersion}, current {CurrentVersion}",
                id, expectedVersion, current.Version);
            throw new ConcurrencyConflictException(id, current.Version, current.Status);
        }

        // 400 — מעבר לא חוקי לפי מכונת המצבים.
        if (!RequestStatusTransitions.IsValid(current.Status, newStatus))
        {
            logger.LogWarning(
                "Invalid status transition on {RequestId}: {From} -> {To}",
                id, current.Status, newStatus);
            throw new InvalidStatusTransitionException(current.Status, newStatus);
        }

        // עדכון אטומי מותנה-גרסה + audit באותה transaction.
        var updated = await repository.UpdateStatusWithAuditAsync(
            id, expectedVersion, current.Status, newStatus, actor, ct);

        if (updated is null)
        {
            // מישהו עדכן בין הקריאה לכתיבה — מניעת Lost Update.
            var latest = await repository.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException(ValidationMessages.RequestNotFound);

            logger.LogWarning(
                "Concurrency conflict on {RequestId} detected at write: expected version {ExpectedVersion}, current {CurrentVersion}",
                id, expectedVersion, latest.Version);
            throw new ConcurrencyConflictException(id, latest.Version, latest.Status);
        }

        // הספירות השתנו — ביטול הסיכום השמור (Task 5.2). רק אחרי commit מוצלח.
        summaryCache.Invalidate();

        logger.LogInformation(
            "Request {RequestId} status changed {From} -> {To} (version {Version})",
            id, current.Status, newStatus, updated.Version);

        return RequestMapper.ToListItemDto(updated);
    }

    public async Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(
        BulkUpdateStatusRequest request,
        CancellationToken ct = default)
    {
        await bulkUpdateValidator.ValidateAndThrowAsync(request, ct);

        var newStatus = request.NewStatus!.Value;
        var actor = string.IsNullOrWhiteSpace(request.ChangedBy) ? DefaultActor : request.ChangedBy.Trim();

        // נרמול מזהים לצורה הקנונית (hex קטן) כדי שהתאמה מול ה-DB לא תיכשל על אותיות גדולות.
        var items = request.Items!
            .Select(i => (Id: ObjectId.Parse(i.Id!.Trim()).ToString(), ExpectedVersion: i.ExpectedVersion!.Value))
            .ToList();

        logger.LogInformation(
            "Bulk status update requested: {Count} requests -> {NewStatus} by {Actor}",
            items.Count, newStatus, actor);

        // מצב נוכחי של כל הפניות בשאילתה אחת.
        var currentById = (await repository.GetByIdsAsync(items.Select(i => i.Id).ToList(), ct))
            .ToDictionary(m => m.Id);

        var results = new List<BulkStatusItemResultDto>(items.Count);
        try
        {
            foreach (var (id, expectedVersion) in items)
            {
                // ביטול עוצר את העיבוד; פניות שכבר עודכנו נשארות מעודכנות (כל אחת ב-transaction משלה).
                ct.ThrowIfCancellationRequested();
                results.Add(await ProcessBulkItemAsync(id, expectedVersion, newStatus, actor, currentById, ct));
            }
        }
        finally
        {
            // invalidation פעם אחת לכל ה-batch, ורק אם משהו עודכן בפועל — גם אם העיבוד נקטע באמצע (Task 5.2).
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

    /// <summary>
    /// עיבוד פנייה בודדת ב-bulk. אותם כללים כמו בעדכון בודד, אבל מחזיר תוצאה במקום לזרוק —
    /// כך כשלון של פנייה אחת לא עוצר את האחרות (Partial Success).
    /// </summary>
    private async Task<BulkStatusItemResultDto> ProcessBulkItemAsync(
        string id,
        int expectedVersion,
        RequestStatus newStatus,
        string actor,
        IReadOnlyDictionary<string, Dal.Models.RequestModel> currentById,
        CancellationToken ct)
    {
        if (!currentById.TryGetValue(id, out var current))
        {
            return new BulkStatusItemResultDto(id, BulkItemOutcome.NotFound, null, null, ValidationMessages.RequestNotFound);
        }

        if (current.Version != expectedVersion)
        {
            logger.LogWarning(
                "Bulk: concurrency conflict on {RequestId}: expected version {ExpectedVersion}, current {CurrentVersion}",
                id, expectedVersion, current.Version);
            return new BulkStatusItemResultDto(
                id, BulkItemOutcome.Conflict, current.Version, current.Status, ValidationMessages.ConcurrencyConflict);
        }

        if (!RequestStatusTransitions.IsValid(current.Status, newStatus))
        {
            return new BulkStatusItemResultDto(
                id,
                BulkItemOutcome.InvalidTransition,
                current.Version,
                current.Status,
                string.Format(ValidationMessages.InvalidTransition, current.Status, newStatus));
        }

        var updated = await repository.UpdateStatusWithAuditAsync(
            id, expectedVersion, current.Status, newStatus, actor, ct);

        if (updated is null)
        {
            // עדכון מתחרה בין הקריאה המרוכזת לכתיבה.
            var latest = await repository.GetByIdAsync(id, ct);
            if (latest is null)
            {
                return new BulkStatusItemResultDto(id, BulkItemOutcome.NotFound, null, null, ValidationMessages.RequestNotFound);
            }

            logger.LogWarning(
                "Bulk: concurrency conflict on {RequestId} detected at write: expected version {ExpectedVersion}, current {CurrentVersion}",
                id, expectedVersion, latest.Version);
            return new BulkStatusItemResultDto(
                id, BulkItemOutcome.Conflict, latest.Version, latest.Status, ValidationMessages.ConcurrencyConflict);
        }

        return new BulkStatusItemResultDto(id, BulkItemOutcome.Updated, updated.Version, updated.Status, null);
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

    /// <summary>מזהה חייב להיות ObjectId תקין — אחרת 400 (ולא חריגת serialization → 500).</summary>
    private static void EnsureValidId(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            throw new ValidationException([new ValidationFailure("Id", ValidationMessages.InvalidId)]);
        }
    }
}

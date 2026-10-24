using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Bl.Interfaces;
using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Common;
using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// API שליפת פניות. ולידציה והחזרת שגיאות מטופלות ב-BL / global handler —
/// ה-controller לא תופס exceptions.
/// </summary>
[ApiController]
[Route("api/requests")]
public sealed class RequestsController(IRequestService requestService) : ControllerBase
{
    /// <summary>שליפת פניות עם דפדוף server-side, סינון, חיפוש ומיון.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<PagedResultDto<RequestListItemDto>>>> Get(
        [FromQuery] RequestSearchRequest request,
        CancellationToken ct)
    {
        var result = await requestService.SearchAsync(request, ct);
        return Ok(ApiResponseDto<PagedResultDto<RequestListItemDto>>.Ok(result));
    }

    /// <summary>סיכום aggregations: ספירה לפי סטטוס ולפי עדיפות.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponseDto<RequestsSummaryDto>>> GetSummary(CancellationToken ct)
    {
        var summary = await requestService.GetSummaryAsync(ct);
        return Ok(ApiResponseDto<RequestsSummaryDto>.Ok(summary));
    }

    /// <summary>פנייה בודדת (משמש גם לרענון אחרי 409).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<RequestListItemDto>>> GetById(string id, CancellationToken ct)
    {
        var request = await requestService.GetByIdAsync(id, ct);
        return request is null
            ? NotFound(ApiResponseDto<RequestListItemDto>.Fail(ValidationMessages.RequestNotFound))
            : Ok(ApiResponseDto<RequestListItemDto>.Ok(request));
    }

    /// <summary>
    /// עדכון סטטוס עם Optimistic Concurrency.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponseDto<RequestListItemDto>>> UpdateStatus(
        string id,
        [FromBody] UpdateRequestStatusRequest request,
        CancellationToken ct)
    {
        var updated = await requestService.UpdateStatusAsync(id, request, ct);
        return Ok(ApiResponseDto<RequestListItemDto>.Ok(updated));
    }

    /// <summary>
    /// עדכון סטטוס מרוכז (עד 100 פניות) עם Partial Success.
    /// 200 – מוחזר דוח פר-פנייה (Updated/NotFound/Conflict/InvalidTransition), גם אם חלק נכשלו;
    /// 400 – הבקשה עצמה לא תקינה (ריקה, מעל 100, כפילויות, מזהה/גרסה חסרים, סטטוס יעד לא אפשרי).
    /// </summary>
    [HttpPatch("bulk-status")]
    public async Task<ActionResult<ApiResponseDto<BulkUpdateStatusResultDto>>> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusRequest request,
        CancellationToken ct)
    {
        var result = await requestService.BulkUpdateStatusAsync(request, ct);
        return Ok(ApiResponseDto<BulkUpdateStatusResultDto>.Ok(result));
    }

    /// <summary>היסטוריית שינויי סטטוס, מהחדש לישן.</summary>
    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponseDto<IReadOnlyList<RequestStatusHistoryItemDto>>>> GetHistory(
        string id,
        CancellationToken ct)
    {
        var history = await requestService.GetHistoryAsync(id, ct);
        return history is null
            ? NotFound(ApiResponseDto<IReadOnlyList<RequestStatusHistoryItemDto>>.Fail(ValidationMessages.RequestNotFound))
            : Ok(ApiResponseDto<IReadOnlyList<RequestStatusHistoryItemDto>>.Ok(history));
    }

}

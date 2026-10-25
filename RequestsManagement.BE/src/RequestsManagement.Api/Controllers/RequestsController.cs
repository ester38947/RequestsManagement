using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Bl.Interfaces;
using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Common;
using RequestsManagement.Dto.Paging;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// Requests API. Validation and error responses are handled in the BL / global handler —
/// the controller does not catch exceptions.
/// </summary>
[ApiController]
[Route("api/requests")]
public sealed class RequestsController(IRequestService requestService) : ControllerBase
{
    /// <summary>Fetches requests with server-side paging, filtering, search and sorting.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<PagedResultDto<RequestListItemDto>>>> Get(
        [FromQuery] RequestSearchRequest request,
        CancellationToken ct)
    {
        var result = await requestService.SearchAsync(request, ct);
        return Ok(ApiResponseDto<PagedResultDto<RequestListItemDto>>.Ok(result));
    }

    /// <summary>Aggregations summary: counts by status and by priority.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponseDto<RequestsSummaryDto>>> GetSummary(CancellationToken ct)
    {
        var summary = await requestService.GetSummaryAsync(ct);
        return Ok(ApiResponseDto<RequestsSummaryDto>.Ok(summary));
    }

    /// <summary>A single request (also used to refresh after a 409).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<RequestListItemDto>>> GetById(string id, CancellationToken ct)
    {
        var request = await requestService.GetByIdAsync(id, ct);
        return request is null
            ? NotFound(ApiResponseDto<RequestListItemDto>.Fail(ValidationMessages.RequestNotFound))
            : Ok(ApiResponseDto<RequestListItemDto>.Ok(request));
    }

    /// <summary>
    /// Status update with optimistic concurrency.
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
    /// Bulk status update (up to 100 requests) with partial success.
    /// 200 - returns a per-item report (Updated/NotFound/Conflict/InvalidTransition), even if some failed;
    /// 400 - the request itself is invalid (empty, over 100, duplicates, missing id/version, unreachable target status).
    /// </summary>
    [HttpPatch("bulk-status")]
    public async Task<ActionResult<ApiResponseDto<BulkUpdateStatusResultDto>>> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusRequest request,
        CancellationToken ct)
    {
        var result = await requestService.BulkUpdateStatusAsync(request, ct);
        return Ok(ApiResponseDto<BulkUpdateStatusResultDto>.Ok(result));
    }

    /// <summary>Status-change history, newest to oldest.</summary>
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

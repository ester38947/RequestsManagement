using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Bl.Caching;
using RequestsManagement.Dal.Seed;
using RequestsManagement.Dto.Common;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// Demo data seeding. Available in the Development environment only.
/// POST /api/seed?count=100000&amp;reset=true -> deletes the existing data and creates exactly 100K requests
/// (a repeatable recreation of the test data). Without reset, it adds to the existing data.
/// </summary>
[ApiController]
[Route("api/seed")]
public sealed class SeedController(
    ISeeder seeder,
    IRequestsSummaryCache summaryCache,
    IHostEnvironment environment,
    ILogger<SeedController> logger) : ControllerBase
{
    private const int DefaultCount = 100_000;
    private const int MaxCount = 1_000_000;

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<SeedResultDto>>> Seed(
        [FromQuery] int count = DefaultCount,
        [FromQuery] bool reset = false,
        CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound(ApiResponseDto<SeedResultDto>.Fail("הזנת נתונים זמינה בסביבת פיתוח בלבד."));
        }

        if (count is <= 0 or > MaxCount)
        {
            return BadRequest(ApiResponseDto<SeedResultDto>.Fail($"count חייב להיות בין 1 ל-{MaxCount}."));
        }

        logger.LogInformation("Seed requested: {Count} requests (reset={Reset}).", count, reset);

        long inserted;
        try
        {
            if (reset)
            {
                await seeder.ResetAsync(ct);
            }

            inserted = await seeder.SeedRequestsAsync(count, ct);
        }
        finally
        {
            // The counts changed (even if the seed was interrupted after some batches).
            summaryCache.Invalidate();
        }

        var total = await seeder.CountRequestsAsync(ct);

        return Ok(ApiResponseDto<SeedResultDto>.Ok(new SeedResultDto(inserted, total, reset)));
    }

    [HttpGet("count")]
    public async Task<ActionResult<ApiResponseDto<long>>> Count(CancellationToken ct)
    {
        var total = await seeder.CountRequestsAsync(ct);
        return Ok(ApiResponseDto<long>.Ok(total));
    }
}

public sealed record SeedResultDto(long Inserted, long TotalInCollection, bool Reset);

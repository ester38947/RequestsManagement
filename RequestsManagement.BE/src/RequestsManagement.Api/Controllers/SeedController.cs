using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Bl.Caching;
using RequestsManagement.Dal.Seed;
using RequestsManagement.Dto.Common;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// הזנת נתוני דמו (Task 1.3). זמין בסביבת Development בלבד.
/// POST /api/seed?count=100000  → מוסיף 100K פניות (ניתן להרצה חוזרת).
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

        logger.LogInformation("Seed requested: {Count} requests.", count);

        long inserted;
        try
        {
            inserted = await seeder.SeedRequestsAsync(count, ct);
        }
        finally
        {
            // הספירות השתנו (גם אם ה-seed נקטע אחרי חלק מה-batches).
            summaryCache.Invalidate();
        }

        var total = await seeder.CountRequestsAsync(ct);

        return Ok(ApiResponseDto<SeedResultDto>.Ok(new SeedResultDto(inserted, total)));
    }

    [HttpGet("count")]
    public async Task<ActionResult<ApiResponseDto<long>>> Count(CancellationToken ct)
    {
        var total = await seeder.CountRequestsAsync(ct);
        return Ok(ApiResponseDto<long>.Ok(total));
    }
}

public sealed record SeedResultDto(long Inserted, long TotalInCollection);

using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dto.Common;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// Basic liveness check: verifies the app is up and has a live connection to MongoDB (ping).
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(AppMongoDbContext context, ILogger<HealthController> logger)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<HealthStatusDto>>> Get(CancellationToken ct)
    {
        logger.LogInformation("Health check requested.");

        var pingResult = await context.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: ct);

        var mongoOk = pingResult.GetValue("ok", 0).ToDouble() == 1d;

        var status = new HealthStatusDto(
            Status: mongoOk ? "Healthy" : "Degraded",
            MongoConnected: mongoOk,
            Database: context.Database.DatabaseNamespace.DatabaseName,
            TimestampUtc: DateTime.UtcNow);

        return Ok(ApiResponseDto<HealthStatusDto>.Ok(status));
    }
}

public sealed record HealthStatusDto(
    string Status,
    bool MongoConnected,
    string Database,
    DateTime TimestampUtc);

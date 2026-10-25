using RequestsManagement.Dal.Indexes;

namespace RequestsManagement.Api.Startup;

/// <summary>
/// Ensures the indexes exist at startup (ensure-on-startup).
/// Runs once as the application starts.
/// </summary>
public sealed class IndexInitializationHostedService(
    IIndexInitializer indexInitializer,
    ILogger<IndexInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await indexInitializer.EnsureIndexesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // An index failure does not bring the app down — it is logged and handled; queries still work (slower).
            logger.LogError(ex, "Failed to ensure MongoDB indexes at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

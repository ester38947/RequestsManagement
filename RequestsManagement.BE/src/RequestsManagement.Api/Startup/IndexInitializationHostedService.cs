using RequestsManagement.Dal.Indexes;

namespace RequestsManagement.Api.Startup;

/// <summary>
/// מוודא שהאינדקסים קיימים ב-startup (ensure-on-startup, Task 1.2).
/// רץ פעם אחת כשהאפליקציה עולה.
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
            // כשל אינדוקס לא מפיל את האפליקציה — נרשם ויטופל, השאילתות עדיין יעבדו (איטי יותר).
            logger.LogError(ex, "Failed to ensure MongoDB indexes at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

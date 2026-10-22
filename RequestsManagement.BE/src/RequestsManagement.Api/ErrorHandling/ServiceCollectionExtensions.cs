namespace RequestsManagement.Api.ErrorHandling;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// רושם את ה-IExceptionHandler הגלובלי יחד עם ProblemDetails (RFC 7807).
    /// </summary>
    public static IServiceCollection AddGlobalErrorHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}

using Microsoft.AspNetCore.Mvc;

namespace RequestsManagement.Api.ErrorHandling;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the global IExceptionHandler together with ProblemDetails (RFC 7807),
    /// and unifies the 400 response for model-binding errors into the same format (see InvalidModelStateResponse).
    /// </summary>
    public static IServiceCollection AddGlobalErrorHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create);

        return services;
    }
}

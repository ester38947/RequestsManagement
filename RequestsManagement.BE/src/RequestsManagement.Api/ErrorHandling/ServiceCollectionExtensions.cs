using Microsoft.AspNetCore.Mvc;

namespace RequestsManagement.Api.ErrorHandling;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// רושם את ה-IExceptionHandler הגלובלי יחד עם ProblemDetails (RFC 7807),
    /// ומאחד את תשובת ה-400 של שגיאות model binding לאותו פורמט (ראו InvalidModelStateResponse).
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

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace RequestsManagement.Bl.Extensions;

/// <summary>
/// הרכבת שכבת ה-BL: services (Scoped), validators (assembly scan) ו-IMemoryCache.
/// Services קונקרטיים יתווספו ב-Epics הבאים.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBl(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<BlAssemblyMarker>();
        services.AddMemoryCache();

        // Singleton: מחזיק את ה-generation counter ומשותף לכל הבקשות.
        services.AddSingleton<Caching.IRequestsSummaryCache, Caching.RequestsSummaryCache>();
        services.AddScoped<Interfaces.IRequestService, Services.RequestService>();

        return services;
    }
}

/// <summary>סמן assembly עבור סריקת validators ב-FluentValidation.</summary>
public sealed class BlAssemblyMarker;

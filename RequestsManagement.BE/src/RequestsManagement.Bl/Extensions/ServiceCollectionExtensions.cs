using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace RequestsManagement.Bl.Extensions;

/// <summary>
/// Wires up the BL layer: services (Scoped), validators (assembly scan) and IMemoryCache.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBl(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<BlAssemblyMarker>();
        services.AddMemoryCache();

        // Singleton: holds the generation counter and is shared across all requests.
        services.AddSingleton<Caching.IRequestsSummaryCache, Caching.RequestsSummaryCache>();
        services.AddScoped<Interfaces.IRequestService, Services.RequestService>();

        return services;
    }
}

/// <summary>Assembly marker for FluentValidation's validator scan.</summary>
public sealed class BlAssemblyMarker;

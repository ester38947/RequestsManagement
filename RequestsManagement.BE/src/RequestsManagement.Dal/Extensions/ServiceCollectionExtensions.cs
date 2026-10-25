using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Indexes;
using RequestsManagement.Dal.Seed;
using RequestsManagement.Dal.Settings;

namespace RequestsManagement.Dal.Extensions;

/// <summary>
/// Wires up the DAL layer: binds Mongo settings, registers the factory and context as Singletons,
/// the index initializer and seeder, and the repositories (Scoped).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDal(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString), "Mongo:ConnectionString is required.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.DatabaseName), "Mongo:DatabaseName is required.");

        services.AddSingleton<MongoConnectionFactory>();
        services.AddSingleton<AppMongoDbContext>();

        services.AddSingleton<IIndexInitializer, MongoIndexInitializer>();
        services.AddScoped<ISeeder, RequestSeeder>();
        services.AddScoped<Interfaces.IRequestRepository, Repositories.RequestRepository>();
        services.AddScoped<Interfaces.IStatusAuditRepository, Repositories.StatusAuditRepository>();

        return services;
    }
}

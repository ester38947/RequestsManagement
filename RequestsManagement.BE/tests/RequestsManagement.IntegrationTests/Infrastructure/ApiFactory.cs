using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using RequestsManagement.Dal.Context;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.IntegrationTests.Infrastructure;

/// <summary>
/// מריץ את ה-API האמיתי בזיכרון מול MongoDB אמיתי (ה-replica set מ-docker-compose).
/// כל מחלקת בדיקות מקבלת database נפרד וזמני שנמחק בסוף — לא נוגעים בנתוני ה-100K.
///
/// Connection string: משתנה הסביבה MONGO_TEST_CONNECTION_STRING, או ברירת המחדל של docker-compose.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultConnectionString = "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string DatabaseName { get; } = $"requestsManagement_it_{Guid.NewGuid():N}";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("MONGO_TEST_CONNECTION_STRING") is { Length: > 0 } cs
            ? cs
            : DefaultConnectionString;

    public AppMongoDbContext Db => Services.GetRequiredService<AppMongoDbContext>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = ConnectionString,
            ["Mongo:DatabaseName"] = DatabaseName,
        }));
    }

    public Task InitializeAsync()
    {
        // יצירת הלקוח מפעילה את ה-host — כולל יצירת האינדקסים (hosted service) ב-database של הבדיקה.
        _ = CreateClient();
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await Db.Client.DropDatabaseAsync(DatabaseName);
        await base.DisposeAsync();
    }

    public async Task<RequestModel> InsertAsync(
        RequestStatus status,
        RequestPriority priority = RequestPriority.Medium,
        string title = "Integration request",
        string organization = "Org A",
        DateTime? createdAt = null,
        int version = 0)
    {
        var created = createdAt ?? DateTime.UtcNow.AddDays(-1);
        var model = new RequestModel
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = title,
            OrganizationName = organization,
            Status = status,
            Priority = priority,
            AssignedTo = "Tester",
            CreatedAt = created,
            UpdatedAt = created,
            Version = version,
        };
        await Db.Requests.InsertOneAsync(model);
        return model;
    }
}

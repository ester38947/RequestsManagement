using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RequestsManagement.Dal.Settings;

namespace RequestsManagement.Dal.Context;

/// <summary>
/// עוטף <see cref="MongoClient"/> יחיד (Singleton). ה-connection string נטען מ-config/env.
/// בעבודה המקורית הוא נמשך מ-GCP Secret Manager — כאן זהו connection string רגיל.
/// </summary>
public sealed class MongoConnectionFactory
{
    private readonly IMongoClient _client;

    public MongoConnectionFactory(IOptions<MongoDbSettings> options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        // כשל מהיר אם ה-DB לא זמין, במקום להיתקע על ברירת המחדל (~30s).
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
    }

    public IMongoClient Client => _client;

    public IMongoDatabase GetDatabase(string name) => _client.GetDatabase(name);
}

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RequestsManagement.Dal.Settings;

namespace RequestsManagement.Dal.Context;

/// <summary>
/// Wraps a single <see cref="MongoClient"/> (Singleton). The connection string is loaded from
/// config/env (a plain connection string here, rather than from a secret manager).
/// </summary>
public sealed class MongoConnectionFactory
{
    private readonly IMongoClient _client;

    public MongoConnectionFactory(IOptions<MongoDbSettings> options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        // Fail fast if the DB is unavailable, instead of waiting out the default (~30s).
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
    }

    public IMongoClient Client => _client;

    public IMongoDatabase GetDatabase(string name) => _client.GetDatabase(name);
}

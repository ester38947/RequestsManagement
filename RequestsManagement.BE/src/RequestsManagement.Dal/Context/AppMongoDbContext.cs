using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dal.Settings;

namespace RequestsManagement.Dal.Context;

/// <summary>
/// Central DB access point. Injected with the singleton <see cref="MongoConnectionFactory"/>,
/// resolves the database by name from settings, and exposes typed collections.
/// Registered as a Singleton.
/// </summary>
public sealed class AppMongoDbContext
{
    private readonly MongoDbSettings _settings;

    public AppMongoDbContext(MongoConnectionFactory factory, IOptions<MongoDbSettings> options)
    {
        _settings = options.Value;
        Database = factory.GetDatabase(_settings.DatabaseName);
        Client = factory.Client;
    }

    /// <summary>The client, needed to open sessions for transactions (bulk + audit).</summary>
    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    /// <summary>The requests collection (typed).</summary>
    public IMongoCollection<RequestModel> Requests =>
        Database.GetCollection<RequestModel>(_settings.Collections.Requests);

    /// <summary>The status-change history collection (typed).</summary>
    public IMongoCollection<StatusAuditEntry> StatusAudit =>
        Database.GetCollection<StatusAuditEntry>(_settings.Collections.StatusAudit);
}

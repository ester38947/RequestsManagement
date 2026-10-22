using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dal.Settings;

namespace RequestsManagement.Dal.Context;

/// <summary>
/// נקודת גישה מרכזית ל-DB. מוזרק עם ה-<see cref="MongoConnectionFactory"/> ה-Singleton,
/// פותר את ה-database לפי שם מה-settings, וחושף collections טיפוסיים.
/// נרשם כ-Singleton.
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

    /// <summary>ה-client, נחוץ לפתיחת sessions ל-transactions (bulk + audit).</summary>
    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    /// <summary>collection הפניות (typed).</summary>
    public IMongoCollection<RequestModel> Requests =>
        Database.GetCollection<RequestModel>(_settings.Collections.Requests);

    /// <summary>collection היסטוריית שינויי הסטטוס (typed).</summary>
    public IMongoCollection<StatusAuditEntry> StatusAudit =>
        Database.GetCollection<StatusAuditEntry>(_settings.Collections.StatusAudit);
}

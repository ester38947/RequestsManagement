namespace RequestsManagement.Dal.Settings;

/// <summary>
/// MongoDB connection settings. Loaded from appsettings.json / env var under the "Mongo" key.
/// The DB and collection names are kept here as constants.
/// </summary>
public sealed class MongoDbSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = default!;

    public string DatabaseName { get; set; } = default!;

    public CollectionNames Collections { get; set; } = new();

    public sealed class CollectionNames
    {
        public string Requests { get; set; } = "requests";

        public string StatusAudit { get; set; } = "statusAudit";
    }
}

namespace RequestsManagement.Dal.Settings;

/// <summary>
/// הגדרות חיבור ל-MongoDB. נטענות מ-appsettings.json / env var תחת המפתח "Mongo".
/// שמות ה-DB וה-collections מרוכזים כאן כקבועים.
/// </summary>
public sealed class MongoDbSettings
{
    /// <summary>שם ה-section ב-configuration.</summary>
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

namespace RequestsManagement.Dal.Indexes;

/// <summary>
/// יוצר/מוודא אינדקסים ב-startup. יישום אידמפוטנטי — יצירה חוזרת של אותו אינדקס לא נכשלת.
/// </summary>
public interface IIndexInitializer
{
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

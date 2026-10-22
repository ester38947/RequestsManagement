namespace RequestsManagement.Dal.Seed;

/// <summary>
/// מזין נתוני דמו ל-collection הפניות ב-bulk. ניתן להרצה חוזרת.
/// </summary>
public interface ISeeder
{
    /// <summary>מזין <paramref name="count"/> פניות חדשות ומחזיר כמה נוצרו בפועל.</summary>
    Task<long> SeedRequestsAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// מוחק את כל הפניות ואת היסטוריית השינויים, כדי שה-seed הבא ייצור את נתוני הבדיקה מחדש.
    /// האינדקסים נשמרים (DeleteMany ולא Drop).
    /// </summary>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>מחזיר את מספר הפניות הקיים כרגע.</summary>
    Task<long> CountRequestsAsync(CancellationToken ct = default);
}

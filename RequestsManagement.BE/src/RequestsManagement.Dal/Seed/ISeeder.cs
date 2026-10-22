namespace RequestsManagement.Dal.Seed;

/// <summary>
/// מזין נתוני דמו ל-collection הפניות ב-bulk. ניתן להרצה חוזרת (כל הרצה מוסיפה batch נוסף).
/// </summary>
public interface ISeeder
{
    /// <summary>מזין <paramref name="count"/> פניות חדשות ומחזיר כמה נוצרו בפועל.</summary>
    Task<long> SeedRequestsAsync(int count, CancellationToken ct = default);

    /// <summary>מחזיר את מספר הפניות הקיים כרגע.</summary>
    Task<long> CountRequestsAsync(CancellationToken ct = default);
}

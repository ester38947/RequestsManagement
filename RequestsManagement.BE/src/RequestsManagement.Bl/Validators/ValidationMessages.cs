namespace RequestsManagement.Bl.Validators;

/// <summary>Centralized validation and error messages (user-facing, in Hebrew).</summary>
public static class ValidationMessages
{
    // --- Query ---
    public const string PageMin = "מספר העמוד חייב להיות 1 ומעלה.";
    public const string PageSizeRange = "גודל העמוד חייב להיות בין 1 ל-{0}.";
    public const string SortByInvalid = "שדה מיון לא נתמך. שדות מותרים: {0}.";
    public const string CreatedRangeInvalid = "טווח התאריכים אינו תקין: 'מתאריך' חייב להיות קטן או שווה ל'עד תאריך'.";
    public const string SearchTooLong = "מחרוזת החיפוש ארוכה מדי (מקסימום {0} תווים).";

    // --- Id ---
    public const string InvalidId = "מזהה הפנייה אינו בפורמט תקין.";

    // --- Status update ---
    public const string NewStatusRequired = "יש לציין סטטוס חדש.";
    public const string NewStatusInvalid = "הסטטוס החדש אינו ערך מוכר.";
    public const string ExpectedVersionRequired = "יש לציין את גרסת הפנייה (expectedVersion) לצורך זיהוי עדכון מתחרה.";
    public const string ExpectedVersionMin = "גרסת הפנייה חייבת להיות 0 ומעלה.";
    public const string ChangedByTooLong = "שם המבצע ארוך מדי (מקסימום {0} תווים).";

    // --- Bulk ---
    public const string BulkItemsRequired = "יש לשלוח לפחות פנייה אחת לעדכון.";
    public const string BulkTooManyItems = "ניתן לעדכן עד {0} פניות בבקשה אחת.";
    public const string BulkDuplicateIds = "רשימת הפניות מכילה מזהים כפולים.";
    public const string BulkItemNull = "פריט ריק ברשימת הפניות.";
    public const string NewStatusNotReachable = "לא ניתן להעביר פניות לסטטוס {0} — אין מעבר חוקי אליו מאף סטטוס.";

    // --- Domain errors ---
    public const string RequestNotFound = "הפנייה לא נמצאה.";
    public const string InvalidTransition = "מעבר סטטוס לא חוקי: מ-{0} ל-{1}.";
    public const string ConcurrencyConflict = "הפנייה עודכנה בינתיים על ידי משתמש אחר. יש לרענן את הנתונים ולנסות שוב.";
}

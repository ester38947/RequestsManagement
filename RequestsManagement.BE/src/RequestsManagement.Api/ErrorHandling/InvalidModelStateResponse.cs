using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;

namespace RequestsManagement.Api.ErrorHandling;

/// <summary>
/// תשובת 400 לשגיאות model binding — כשהבקשה נכשלת עוד לפני שהגיעה ל-action
/// (JSON שבור, enum לא מוכר כמו "Foo", או "page=abc").
///
/// בלי זה [ApiController] מחזיר את ברירת המחדל של ASP.NET: כותרת באנגלית, שמות שדות בסגנון "$.newStatus",
/// ושגיאה נוספת ומבלבלת על פרמטר ה-action ("The request field is required").
/// כאן מוחזר אותו פורמט כמו בשאר שגיאות הוולידציה (FluentValidation דרך ה-handler הגלובלי):
/// ValidationProblemDetails, כותרת "שגיאות ולידציה", שמות שדות כמו בשאר ה-API והודעות בעברית.
/// </summary>
internal static class InvalidModelStateResponse
{
    private const string BodyKey = "body";

    // ערכים מותרים לשדות enum — כדי שההודעה תגיד למשתמש מה כן אפשר לשלוח.
    private static readonly Dictionary<string, string[]> EnumFieldValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NewStatus"] = Enum.GetNames<RequestStatus>(),
        ["Status"] = Enum.GetNames<RequestStatus>(),
        ["Priority"] = Enum.GetNames<RequestPriority>(),
        ["SortDirection"] = Enum.GetNames<SortDirection>(),
    };

    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();

        // שם פרמטר ה-action (למשל "request"): השגיאה עליו היא תוצאה של גוף שלא נקרא, ולא בעיה נפרדת.
        var parameterNames = context.ActionDescriptor.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bodyFailed = context.ModelState.Keys.Any(k => k.Length == 0 || k.StartsWith('$'));

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            if (bodyFailed && parameterNames.Contains(key))
            {
                continue;
            }

            var (field, message) = Describe(key, entry);
            errors[field] = errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "שגיאות ולידציה",
        };

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(InvalidModelStateResponse));
        logger.LogWarning(
            "Handled 400 (model binding) on {Path}: {Fields}",
            context.HttpContext.Request.Path, string.Join(", ", errors.Keys));

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static (string Field, string Message) Describe(string key, ModelStateEntry entry)
    {
        // גוף ריק לגמרי — המפתח הוא "".
        if (key.Length == 0 || key == "$")
        {
            return (BodyKey, "גוף הבקשה חסר או אינו JSON תקין.");
        }

        var field = ToFieldName(key);
        var fromBody = key.StartsWith("$.", StringComparison.Ordinal);

        // מה-body: ערך שלא הומר לטיפוס (למשל "Foo" לסטטוס) לעומת JSON שבור תחבירית.
        if (fromBody && !entry.Errors.Any(e => e.ErrorMessage.Contains("could not be converted", StringComparison.Ordinal)))
        {
            return (BodyKey, "גוף הבקשה אינו JSON תקין.");
        }

        return EnumFieldValues.TryGetValue(field, out var allowed)
            ? (field, $"ערך לא מוכר. ערכים מותרים: {string.Join(", ", allowed)}.")
            : (field, "הערך שנשלח אינו בפורמט תקין.");
    }

    /// <summary>"$.newStatus" → "NewStatus", ‏"$.items[1].expectedVersion" → "Items[1].ExpectedVersion", ‏"Page" נשאר "Page".</summary>
    private static string ToFieldName(string key)
    {
        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Paging;

namespace RequestsManagement.Api.ErrorHandling;

/// <summary>
/// 400 response for model-binding errors — when the request fails before it even reaches the action
/// (broken JSON, an unknown enum like "Foo", or "page=abc").
///
/// Without this, [ApiController] returns the ASP.NET default: an English title, field names like
/// "$.newStatus", and an extra confusing error about the action parameter ("The request field is required").
/// Here we return the same format as the rest of the validation errors (FluentValidation via the global
/// handler): ValidationProblemDetails, the same Hebrew validation title, field names as elsewhere in
/// the API, and messages in Hebrew.
/// </summary>
internal static class InvalidModelStateResponse
{
    private const string BodyKey = "body";

    // Allowed values for enum fields — so the message tells the user what can be sent.
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

        // The action parameter name (e.g. "request"): an error on it is a consequence of the body not being read, not a separate problem.
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
        // A completely empty body — the key is "".
        if (key.Length == 0 || key == "$")
        {
            return (BodyKey, "גוף הבקשה חסר או אינו JSON תקין.");
        }

        var field = ToFieldName(key);
        var fromBody = key.StartsWith("$.", StringComparison.Ordinal);

        // From the body: a value that failed to convert to its type (e.g. "Foo" for a status) versus syntactically broken JSON.
        if (fromBody && !entry.Errors.Any(e => e.ErrorMessage.Contains("could not be converted", StringComparison.Ordinal)))
        {
            return (BodyKey, "גוף הבקשה אינו JSON תקין.");
        }

        return EnumFieldValues.TryGetValue(field, out var allowed)
            ? (field, $"ערך לא מוכר. ערכים מותרים: {string.Join(", ", allowed)}.")
            : (field, "הערך שנשלח אינו בפורמט תקין.");
    }

    /// <summary>"$.newStatus" -> "NewStatus", "$.items[1].expectedVersion" -> "Items[1].ExpectedVersion", "Page" stays "Page".</summary>
    private static string ToFieldName(string key)
    {
        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}

using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Bl.Exceptions;

namespace RequestsManagement.Api.ErrorHandling;

/// <summary>
/// מטפל שגיאות גלובלי (IExceptionHandler, .NET 8+). ממפה חריגות ל-ProblemDetails (RFC 7807):
/// ValidationException/ArgumentException/InvalidStatusTransitionException → 400, KeyNotFoundException → 404,
/// ConcurrencyConflictException → 409,
/// UnauthorizedAccessException → 401, אחרת → 500. 4xx נרשם כ-Warning, 5xx כ-Error.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => BuildValidationProblem(validationException),
            ConcurrencyConflictException conflict => BuildConflictProblem(conflict),
            InvalidStatusTransitionException transition => BuildTransitionProblem(transition),
            ArgumentException argEx => BuildProblem(HttpStatusCode.BadRequest, "בקשה לא תקינה", argEx.Message),
            KeyNotFoundException notFound => BuildProblem(HttpStatusCode.NotFound, "לא נמצא", notFound.Message),
            UnauthorizedAccessException unauthorized => BuildProblem(HttpStatusCode.Unauthorized, "אין הרשאה", unauthorized.Message),
            _ => BuildProblem(HttpStatusCode.InternalServerError, "שגיאת שרת", "אירעה שגיאה בלתי צפויה."),
        };

        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception. Path={Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Handled {StatusCode} on {Path}: {Message}", statusCode, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        // סריאליזציה לפי הטיפוס בזמן-ריצה כדי לכלול שדות של טיפוסים נגזרים (למשל Errors ב-ValidationProblemDetails).
        await httpContext.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), options: null, cancellationToken);

        return true;
    }

    private static ProblemDetails BuildProblem(HttpStatusCode status, string title, string detail) => new()
    {
        Status = (int)status,
        Title = title,
        Detail = detail,
    };

    /// <summary>409 — כולל גרסה וסטטוס עדכניים כדי שהלקוח יוכל לרענן ולנסות שוב.</summary>
    private static ProblemDetails BuildConflictProblem(ConcurrencyConflictException exception)
    {
        var problem = BuildProblem(HttpStatusCode.Conflict, "התנגשות עדכון", exception.Message);
        problem.Extensions["requestId"] = exception.RequestId;
        problem.Extensions["currentVersion"] = exception.CurrentVersion;
        problem.Extensions["currentStatus"] = exception.CurrentStatus.ToString();
        return problem;
    }

    /// <summary>400 — מעבר לא חוקי, כולל המעברים המותרים מהסטטוס הנוכחי.</summary>
    private static ProblemDetails BuildTransitionProblem(InvalidStatusTransitionException exception)
    {
        var problem = BuildProblem(HttpStatusCode.BadRequest, "מעבר סטטוס לא חוקי", exception.Message);
        problem.Extensions["currentStatus"] = exception.From.ToString();
        problem.Extensions["requestedStatus"] = exception.To.ToString();
        problem.Extensions["allowedTransitions"] = exception.Allowed.Select(s => s.ToString()).ToArray();
        return problem;
    }

    private static ValidationProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "שגיאות ולידציה",
        };
    }
}

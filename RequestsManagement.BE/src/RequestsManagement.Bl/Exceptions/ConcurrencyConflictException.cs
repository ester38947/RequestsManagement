using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Bl.Exceptions;

/// <summary>
/// עדכון מתחרה (Optimistic Concurrency): הגרסה שהלקוח שלח אינה הגרסה העדכנית ב-DB.
/// ממופה ל-409 Conflict, כולל הגרסה והסטטוס העדכניים כדי שה-FE יוכל לרענן.
/// </summary>
public sealed class ConcurrencyConflictException(string requestId, int currentVersion, RequestStatus currentStatus)
    : Exception(ValidationMessages.ConcurrencyConflict)
{
    public string RequestId { get; } = requestId;

    public int CurrentVersion { get; } = currentVersion;

    public RequestStatus CurrentStatus { get; } = currentStatus;
}

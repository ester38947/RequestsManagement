namespace RequestsManagement.Dto.Enums;

/// <summary>
/// מכונת מצבים למעברי סטטוס חוקיים של פנייה.
///
/// זרימה:
///   New        → InProgress, Waiting
///   InProgress → Waiting, Completed
///   Waiting    → InProgress, Completed
///   Completed  → (מצב סופי, אין מעברים)
///
/// נשמר כ-API דומיין יחיד כדי שגם ה-BL (ולידציה) וגם בדיקות ישתמשו באותו מקור אמת.
/// </summary>
public static class RequestStatusTransitions
{
    private static readonly IReadOnlyDictionary<RequestStatus, IReadOnlySet<RequestStatus>> Allowed =
        new Dictionary<RequestStatus, IReadOnlySet<RequestStatus>>
        {
            [RequestStatus.New] = new HashSet<RequestStatus> { RequestStatus.InProgress, RequestStatus.Waiting },
            [RequestStatus.InProgress] = new HashSet<RequestStatus> { RequestStatus.Waiting, RequestStatus.Completed },
            [RequestStatus.Waiting] = new HashSet<RequestStatus> { RequestStatus.InProgress, RequestStatus.Completed },
            [RequestStatus.Completed] = new HashSet<RequestStatus>(),
        };

    /// <summary>האם מותר לעבור מ-<paramref name="from"/> ל-<paramref name="to"/>.</summary>
    public static bool IsValid(RequestStatus from, RequestStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// האם קיים מצב כלשהו שממנו מותר לעבור ל-<paramref name="to"/>.
    /// משמש לוולידציה ברמת בקשת bulk (למשל New אינו יעד חוקי לעולם).
    /// </summary>
    public static bool IsReachableTarget(RequestStatus to) =>
        Allowed.Values.Any(targets => targets.Contains(to));

    /// <summary>הסטטוסים אליהם ניתן לעבור מ-<paramref name="from"/> (ריק אם מצב סופי).</summary>
    public static IReadOnlySet<RequestStatus> AllowedFrom(RequestStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : new HashSet<RequestStatus>();
}

namespace RequestsManagement.Dto.Enums;

/// <summary>
/// State machine for valid request status transitions.
///
/// Flow:
///   New        -> InProgress, Waiting
///   InProgress -> Waiting, Completed
///   Waiting    -> InProgress, Completed
///   Completed  -> (terminal state, no transitions)
///
/// Kept as a single domain API so the BL (validation) and the tests use one source of truth.
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

    /// <summary>Whether a transition from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    public static bool IsValid(RequestStatus from, RequestStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Whether any state allows a transition into <paramref name="to"/>.
    /// Used for bulk-request validation (e.g. New is never a valid target).
    /// </summary>
    public static bool IsReachableTarget(RequestStatus to) =>
        Allowed.Values.Any(targets => targets.Contains(to));

    /// <summary>The statuses reachable from <paramref name="from"/> (empty if it is a terminal state).</summary>
    public static IReadOnlySet<RequestStatus> AllowedFrom(RequestStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : new HashSet<RequestStatus>();
}

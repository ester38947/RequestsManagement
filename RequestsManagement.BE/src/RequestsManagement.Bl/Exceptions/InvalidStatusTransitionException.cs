using RequestsManagement.Bl.Validators;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Bl.Exceptions;

/// <summary>A status transition not allowed by the state machine. Mapped to 400.</summary>
public sealed class InvalidStatusTransitionException(RequestStatus from, RequestStatus to)
    : Exception(string.Format(ValidationMessages.InvalidTransition, from, to))
{
    public RequestStatus From { get; } = from;

    public RequestStatus To { get; } = to;

    public IReadOnlySet<RequestStatus> Allowed { get; } = RequestStatusTransitions.AllowedFrom(from);
}

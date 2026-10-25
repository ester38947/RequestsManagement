using FluentValidation;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Validators;

/// <summary>
/// Validates the status-update body. The transition itself is checked in the service against the current status in the DB.
/// </summary>
public sealed class UpdateRequestStatusRequestValidator : AbstractValidator<UpdateRequestStatusRequest>
{
    public UpdateRequestStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(ValidationMessages.NewStatusRequired)
            .IsInEnum()
            .WithMessage(ValidationMessages.NewStatusInvalid)
            .WithName("סטטוס חדש");

        RuleFor(x => x.ExpectedVersion)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(ValidationMessages.ExpectedVersionRequired)
            .GreaterThanOrEqualTo(0)
            .WithMessage(ValidationMessages.ExpectedVersionMin)
            .WithName("גרסה");

        RuleFor(x => x.ChangedBy)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(ValidationConstants.MaxChangedByLength)
            .WithMessage(string.Format(ValidationMessages.ChangedByTooLong, ValidationConstants.MaxChangedByLength))
            .WithName("מבצע השינוי")
            .When(x => !string.IsNullOrEmpty(x.ChangedBy));
    }
}

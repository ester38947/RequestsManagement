using FluentValidation;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Validators;

/// <summary>
/// ולידציה לגוף עדכון הסטטוס (Task 3.1/3.2). חוקיות המעבר עצמו נבדקת ב-service מול הסטטוס הנוכחי ב-DB.
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

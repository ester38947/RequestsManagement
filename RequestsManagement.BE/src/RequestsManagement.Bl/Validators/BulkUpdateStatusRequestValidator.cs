using FluentValidation;
using MongoDB.Bson;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Validators;

/// <summary>
/// Request-level validation for bulk — a failure here returns 400 for the whole request and no item is
/// processed: non-empty list, up to 100 items, no duplicates, valid ids, a version per item, and a
/// reachable target status. Each item's transition validity is checked per item against its own current
/// status (an InvalidTransition result in the report).
/// </summary>
public sealed class BulkUpdateStatusRequestValidator : AbstractValidator<BulkUpdateStatusRequest>
{
    public BulkUpdateStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(ValidationMessages.NewStatusRequired)
            .IsInEnum()
            .WithMessage(ValidationMessages.NewStatusInvalid)
            .Must(s => RequestStatusTransitions.IsReachableTarget(s!.Value))
            .WithMessage(x => string.Format(ValidationMessages.NewStatusNotReachable, x.NewStatus))
            .WithName("סטטוס חדש");

        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(ValidationMessages.BulkItemsRequired)
            .NotEmpty()
            .WithMessage(ValidationMessages.BulkItemsRequired)
            .Must(items => items!.Count <= BulkUpdateStatusRequest.MaxItems)
            .WithMessage(string.Format(ValidationMessages.BulkTooManyItems, BulkUpdateStatusRequest.MaxItems))
            .Must(HaveNoDuplicateIds)
            .WithMessage(ValidationMessages.BulkDuplicateIds)
            .WithName("פניות");

        // Per-item validation only when the list is a reasonable size (avoids thousands of error messages on an oversized list).
        When(x => x.Items is { Count: > 0 and <= BulkUpdateStatusRequest.MaxItems }, () =>
        {
            // No Cascade(Stop) at the collection level: on RuleForEach it would stop after the first bad item
            // and hide the errors of the rest. Cascade is kept inside each item's rules.
            RuleForEach(x => x.Items)
                .NotNull()
                .WithMessage(ValidationMessages.BulkItemNull)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.Id)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty()
                        .WithMessage(ValidationMessages.InvalidId)
                        .Must(id => ObjectId.TryParse(id, out _))
                        .WithMessage(ValidationMessages.InvalidId)
                        .WithName("מזהה");

                    item.RuleFor(i => i.ExpectedVersion)
                        .Cascade(CascadeMode.Stop)
                        .NotNull()
                        .WithMessage(ValidationMessages.ExpectedVersionRequired)
                        .GreaterThanOrEqualTo(0)
                        .WithMessage(ValidationMessages.ExpectedVersionMin)
                        .WithName("גרסה");
                });
        });

        RuleFor(x => x.ChangedBy)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(ValidationConstants.MaxChangedByLength)
            .WithMessage(string.Format(ValidationMessages.ChangedByTooLong, ValidationConstants.MaxChangedByLength))
            .WithName("מבצע השינוי")
            .When(x => !string.IsNullOrEmpty(x.ChangedBy));
    }

    /// <summary>Duplicates are checked on the normalized id (lowercase ObjectId hex).</summary>
    private static bool HaveNoDuplicateIds(List<BulkUpdateStatusItem>? items)
    {
        var ids = items!
            .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Id))
            .Select(i => i.Id!.Trim().ToLowerInvariant())
            .ToList();

        return ids.Count == ids.Distinct().Count();
    }
}

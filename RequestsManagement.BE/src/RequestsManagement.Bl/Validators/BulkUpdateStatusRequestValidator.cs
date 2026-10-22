using FluentValidation;
using MongoDB.Bson;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Validators;

/// <summary>
/// ולידציה ברמת בקשת ה-bulk (Task 4.2) — כשל כאן מחזיר 400 לכל הבקשה ולא מעובדת אף פנייה:
/// רשימה לא ריקה, עד 100 פניות, ללא כפילויות, מזהים תקינים, גרסה לכל פנייה, וסטטוס יעד שאפשר להגיע אליו.
/// חוקיות המעבר לכל פנייה נבדקת פר-פנייה מול הסטטוס הנוכחי שלה (תוצאת InvalidTransition בדוח).
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

        // ולידציה פר-פריט רק כשהרשימה בגודל סביר (מונע אלפי הודעות שגיאה על רשימה חריגה).
        When(x => x.Items is { Count: > 0 and <= BulkUpdateStatusRequest.MaxItems }, () =>
        {
            // בלי Cascade(Stop) ברמת האוסף: ב-RuleForEach הוא עוצר אחרי הפריט השגוי הראשון
            // ומסתיר שגיאות של שאר הפריטים. ה-Cascade נשמר בתוך חוקי כל פריט.
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

    /// <summary>כפילות נבדקת על המזהה המנורמל (ObjectId hex באותיות קטנות).</summary>
    private static bool HaveNoDuplicateIds(List<BulkUpdateStatusItem>? items)
    {
        var ids = items!
            .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Id))
            .Select(i => i.Id!.Trim().ToLowerInvariant())
            .ToList();

        return ids.Count == ids.Distinct().Count();
    }
}

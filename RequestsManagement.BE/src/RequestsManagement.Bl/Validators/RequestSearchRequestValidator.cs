using FluentValidation;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.Bl.Validators;

/// <summary>
/// ולידציה לפרמטרי השליפה (Task 2.5): דפדוף, הגבלת PageSize, מיון מ-whitelist, טווח תאריכים, אורך חיפוש.
/// </summary>
public sealed class RequestSearchRequestValidator : AbstractValidator<RequestSearchRequest>
{
    public RequestSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(1)
            .WithName("עמוד")
            .WithMessage(ValidationMessages.PageMin);

        RuleFor(x => x.PageSize)
            .Cascade(CascadeMode.Stop)
            .InclusiveBetween(1, RequestSearchRequest.MaxPageSize)
            .WithName("גודל עמוד")
            .WithMessage(string.Format(ValidationMessages.PageSizeRange, RequestSearchRequest.MaxPageSize));

        RuleFor(x => x.SortBy)
            .Cascade(CascadeMode.Stop)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy)
                || RequestSearchRequest.SortableFields.ContainsKey(sortBy))
            .WithName("מיון")
            .WithMessage(string.Format(
                ValidationMessages.SortByInvalid,
                string.Join(", ", RequestSearchRequest.SortableFields.Keys)));

        RuleFor(x => x.Search)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(ValidationConstants.MaxSearchLength)
            .WithName("חיפוש")
            .WithMessage(string.Format(ValidationMessages.SearchTooLong, ValidationConstants.MaxSearchLength))
            .When(x => !string.IsNullOrEmpty(x.Search));

        RuleFor(x => x)
            .Cascade(CascadeMode.Stop)
            .Must(x => x.CreatedFrom is null || x.CreatedTo is null || x.CreatedFrom <= x.CreatedTo)
            .WithName("טווח תאריכים")
            .WithMessage(ValidationMessages.CreatedRangeInvalid);
    }
}

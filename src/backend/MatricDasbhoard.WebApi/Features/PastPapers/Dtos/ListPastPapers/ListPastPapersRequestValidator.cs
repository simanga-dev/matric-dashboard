using FluentValidation;

namespace MatricDasbhoard.WebApi.Features.PastPapers.Dtos.ListPastPapers;

/// <summary>
/// Validates <see cref="ListPastPapersRequest"/> fields at runtime.
/// </summary>
public class ListPastPapersRequestValidator : AbstractValidator<ListPastPapersRequest>
{
    /// <summary>
    /// Initializes validation rules for past paper list requests.
    /// </summary>
    public ListPastPapersRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Search));

        RuleFor(x => x.Subject)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Subject));

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100)
            .When(x => x.Year is not null);

        RuleFor(x => x.Grade)
            .InclusiveBetween(10, 12)
            .When(x => x.Grade is not null);

        RuleFor(x => x.PaperNumber)
            .InclusiveBetween(1, 4)
            .When(x => x.PaperNumber is not null);

        RuleFor(x => x.Session)
            .IsInEnum()
            .When(x => x.Session is not null);

        RuleFor(x => x.Language)
            .IsInEnum()
            .When(x => x.Language is not null);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .When(x => x.Kind is not null);

        RuleFor(x => x.Sort)
            .IsInEnum();
    }
}

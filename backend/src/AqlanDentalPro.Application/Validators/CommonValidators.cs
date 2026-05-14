using AqlanDentalPro.Application.DTOs.Common;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates paginated request — used across all list endpoints.
/// PageSize cap prevents DoS; SortBy length prevents injection.
/// </summary>
public sealed class PaginatedRequestValidator : AbstractValidator<PaginatedRequest>
{
    public PaginatedRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("رقم الصفحة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("حجم الصفحة يجب أن يكون بين 1 و100");

        RuleFor(x => x.Search)
            .MaximumLength(200).WithMessage("نص البحث يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Search));

        RuleFor(x => x.SortBy)
            .MaximumLength(100).WithMessage("حقل الترتيب يجب ألا يتجاوز 100 حرف")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$").WithMessage("حقل الترتيب يحتوي على أحرف غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));
    }
}

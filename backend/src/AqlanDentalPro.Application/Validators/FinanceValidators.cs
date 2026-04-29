using AqlanDentalPro.Application.DTOs.Finance;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.Specialty)
            .NotEmpty().WithMessage("التخصص مطلوب")
            .MaximumLength(100).WithMessage("التخصص يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("المبلغ الإجمالي يجب أن يكون أكبر من صفر");

        RuleFor(x => x.DownPayment)
            .GreaterThanOrEqualTo(0).WithMessage("الدفعة الأولى يجب أن تكون صفراً أو أكثر")
            .LessThanOrEqualTo(x => x.TotalAmount).WithMessage("الدفعة الأولى يجب ألا تتجاوز المبلغ الإجمالي");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("الخصم يجب أن يكون صفراً أو أكثر")
            .LessThan(x => x.TotalAmount).WithMessage("الخصم يجب أن يكون أقل من المبلغ الإجمالي");

        RuleFor(x => x.InstallmentsCount)
            .InclusiveBetween(0, 60).WithMessage("عدد الأقساط يجب أن يكون بين 0 و60");

        RuleFor(x => x.StartDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ العقد غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.StartDate));
    }
}

public sealed class UpdateContractRequestValidator : AbstractValidator<UpdateContractRequest>
{
    private static readonly HashSet<string> ValidStatuses =
        ["active", "completed", "cancelled", "on_hold"];

    public UpdateContractRequestValidator()
    {
        RuleFor(x => x.Specialty)
            .NotEmpty().WithMessage("التخصص مطلوب")
            .MaximumLength(100).WithMessage("التخصص يجب ألا يتجاوز 100 حرف")
            .When(x => x.Specialty is not null);

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("المبلغ الإجمالي يجب أن يكون أكبر من صفر")
            .When(x => x.TotalAmount.HasValue);

        RuleFor(x => x.DownPayment)
            .GreaterThanOrEqualTo(0).WithMessage("الدفعة الأولى يجب أن تكون صفراً أو أكثر")
            .When(x => x.DownPayment.HasValue);

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("الخصم يجب أن يكون صفراً أو أكثر")
            .When(x => x.DiscountAmount.HasValue);

        RuleFor(x => x.InstallmentsCount)
            .InclusiveBetween(0, 60).WithMessage("عدد الأقساط يجب أن يكون بين 0 و60")
            .When(x => x.InstallmentsCount.HasValue);

        RuleFor(x => x.StartDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ العقد غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.StartDate));

        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s!)).WithMessage("حالة العقد غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    private static readonly HashSet<string> ValidMethods =
        ["cash", "bank_transfer", "card", "check", "other"];

    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("مبلغ الدفعة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("طريقة الدفع مطلوبة")
            .Must(m => m != null && ValidMethods.Contains(m)).WithMessage("طريقة الدفع غير صالحة");
    }
}

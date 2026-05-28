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
            .MaximumLength(100).WithMessage("التخصص يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Specialty));

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

        RuleFor(x => x.ServiceDescription)
            .MaximumLength(500).WithMessage("وصف الخدمة يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ServiceDescription));
    }
}

public sealed class UpdatePaymentRequestValidator : AbstractValidator<UpdatePaymentRequest>
{
    private static readonly HashSet<string> ValidMethods =
        ["cash", "bank_transfer", "card", "check", "other"];

    public UpdatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("مبلغ الدفعة يجب أن يكون أكبر من صفر")
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.PaymentMethod)
            .Must(m => m == null || ValidMethods.Contains(m)).WithMessage("طريقة الدفع غير صالحة");

        RuleFor(x => x.ServiceDescription)
            .MaximumLength(500).WithMessage("وصف الخدمة يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ServiceDescription));
    }
}

public sealed class RefundPaymentRequestValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب الاسترداد مطلوب")
            .MaximumLength(500).WithMessage("سبب الاسترداد يجب ألا يتجاوز 500 حرف");
    }
}

/// <summary>
/// Validates contract update — migrates legacy [Range] DataAnnotations to FluentValidation.
/// Cross-field rule: DiscountAmount must be less than TotalAmount.
/// </summary>
public sealed class UpdateContractRequestValidator : AbstractValidator<UpdateContractRequest>
{
    public UpdateContractRequestValidator()
    {
        RuleFor(x => x.Specialty)
            .MaximumLength(100).WithMessage("التخصص يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Specialty));

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("إجمالي العقد يجب أن يكون صفراً أو أكثر");

        RuleFor(x => x.InstallmentsCount)
            .InclusiveBetween(0, 60).WithMessage("عدد الأقساط يجب أن يكون بين 0 و60");

        RuleFor(x => x.InstallmentAmount)
            .GreaterThanOrEqualTo(0).WithMessage("قيمة القسط يجب أن تكون صفراً أو أكثر")
            .When(x => x.InstallmentAmount.HasValue);

        RuleFor(x => x.StartDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ العقد غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.StartDate));

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("قيمة الخصم يجب أن تكون صفراً أو أكثر")
            .LessThan(x => x.TotalAmount).WithMessage("الخصم يجب أن يكون أقل من المبلغ الإجمالي");

        RuleFor(x => x.DiscountReason)
            .MaximumLength(300).WithMessage("سبب الخصم يجب ألا يتجاوز 300 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.DiscountReason));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

// ─── H5: Invoice validators ─────────────────────────────────────────────────

/// <summary>
/// H5 FIX: Validates CreateInvoiceRequest — prevents negative amounts,
/// zero quantities, empty patient ID, and unbounded strings.
/// </summary>
public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("معرّف المريض مطلوب");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الخصم يجب أن يكون صفراً أو أكثر")
            .When(x => x.DiscountAmount.HasValue);

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الضريبة يجب أن يكون صفراً أو أكثر")
            .When(x => x.TaxAmount.HasValue);

        // V4: التحقق من نسبة الضريبة
        RuleFor(x => x.TaxPercentage)
            .InclusiveBetween(0, 100).WithMessage("نسبة الضريبة يجب أن تكون بين 0 و 100");

        // V4: التحقق من نسبة التغطية المخصصة
        RuleFor(x => x.CustomCoveragePercentage)
            .InclusiveBetween(0, 100).WithMessage("نسبة التغطية التأمينية يجب أن تكون بين 0 و 100")
            .When(x => x.CustomCoveragePercentage.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف")
            .When(x => x.Notes != null);

        RuleForEach(x => x.LineItems)
            .SetValidator(new CreateInvoiceLineItemRequestValidator());
    }
}

public sealed class CreateInvoiceLineItemRequestValidator : AbstractValidator<CreateInvoiceLineItemRequest>
{
    public CreateInvoiceLineItemRequestValidator()
    {
        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("سعر الوحدة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر");

        RuleFor(x => x.ServiceNameSnapshot)
            .MaximumLength(200).WithMessage("اسم الخدمة يجب ألا يتجاوز 200 حرف")
            .When(x => x.ServiceNameSnapshot != null);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("الوصف يجب ألا يتجاوز 500 حرف")
            .When(x => x.Description != null);
    }
}

/// <summary>
/// H5 FIX: Validates UpdateInvoiceRequest — same rules as create but for draft updates.
/// </summary>
public sealed class UpdateInvoiceRequestValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator()
    {
        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الخصم يجب أن يكون صفراً أو أكثر")
            .When(x => x.DiscountAmount.HasValue);

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("مبلغ الضريبة يجب أن يكون صفراً أو أكثر")
            .When(x => x.TaxAmount.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف")
            .When(x => x.Notes != null);

        RuleForEach(x => x.LineItems)
            .SetValidator(new UpdateInvoiceLineItemRequestValidator());
    }
}

public sealed class UpdateInvoiceLineItemRequestValidator : AbstractValidator<UpdateInvoiceLineItemRequest>
{
    public UpdateInvoiceLineItemRequestValidator()
    {
        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("سعر الوحدة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر");

        RuleFor(x => x.ServiceNameSnapshot)
            .MaximumLength(200).WithMessage("اسم الخدمة يجب ألا يتجاوز 200 حرف")
            .When(x => x.ServiceNameSnapshot != null);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("الوصف يجب ألا يتجاوز 500 حرف")
            .When(x => x.Description != null);
    }
}

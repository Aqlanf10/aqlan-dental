using AqlanDentalPro.Application.DTOs.Commission;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates UpdateServiceCommissionDefaultsRequest — ensures costs are non-negative
/// and commission percentage is within [0, 100]. Runs before controller action via
/// FluentValidation auto-validation so invalid input gets 400 instead of reaching
/// the service layer.
/// </summary>
public sealed class UpdateServiceCommissionDefaultsRequestValidator
    : AbstractValidator<UpdateServiceCommissionDefaultsRequest>
{
    public UpdateServiceCommissionDefaultsRequestValidator()
    {
        RuleFor(x => x.DefaultMaterialCost)
            .GreaterThanOrEqualTo(0).WithMessage("تكلفة المواد لا يمكن أن تكون سالبة");

        RuleFor(x => x.DefaultLabCost)
            .GreaterThanOrEqualTo(0).WithMessage("تكلفة المعمل لا يمكن أن تكون سالبة");

        RuleFor(x => x.DefaultDoctorCommissionPercentage)
            .GreaterThanOrEqualTo(0).WithMessage("نسبة العمولة يجب أن تكون بين 0 و 100")
            .When(x => x.DefaultDoctorCommissionPercentage.HasValue);

        RuleFor(x => x.DefaultDoctorCommissionPercentage)
            .LessThanOrEqualTo(100).WithMessage("نسبة العمولة يجب أن تكون بين 0 و 100")
            .When(x => x.DefaultDoctorCommissionPercentage.HasValue);
    }
}

/// <summary>
/// Validates UpdateLineItemCommissionRequest — ensures costs are non-negative
/// and commission percentage is within [0, 100].
/// </summary>
public sealed class UpdateLineItemCommissionRequestValidator
    : AbstractValidator<UpdateLineItemCommissionRequest>
{
    public UpdateLineItemCommissionRequestValidator()
    {
        RuleFor(x => x.MaterialCost)
            .GreaterThanOrEqualTo(0).WithMessage("التكاليف لا يمكن أن تكون سالبة")
            .When(x => x.MaterialCost.HasValue);

        RuleFor(x => x.LabCost)
            .GreaterThanOrEqualTo(0).WithMessage("التكاليف لا يمكن أن تكون سالبة")
            .When(x => x.LabCost.HasValue);

        RuleFor(x => x.OtherDirectCost)
            .GreaterThanOrEqualTo(0).WithMessage("التكاليف لا يمكن أن تكون سالبة")
            .When(x => x.OtherDirectCost.HasValue);

        RuleFor(x => x.DoctorCommissionPercentage)
            .GreaterThanOrEqualTo(0).WithMessage("نسبة عمولة الطبيب يجب أن تكون بين 0 و 100")
            .When(x => x.DoctorCommissionPercentage.HasValue);

        RuleFor(x => x.DoctorCommissionPercentage)
            .LessThanOrEqualTo(100).WithMessage("نسبة عمولة الطبيب يجب أن تكون بين 0 و 100")
            .When(x => x.DoctorCommissionPercentage.HasValue);
    }
}

/// <summary>
/// Validates RecordCommissionPaymentRequest — ensures amount is positive and required fields are set.
/// </summary>
public sealed class RecordCommissionPaymentRequestValidator
    : AbstractValidator<RecordCommissionPaymentRequest>
{
    public RecordCommissionPaymentRequestValidator()
    {
        RuleFor(x => x.DoctorId)
            .NotEmpty().WithMessage("معرّف الطبيب مطلوب");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("مبلغ الدفعة يجب أن يكون أكبر من الصفر");

        RuleFor(x => x.PaymentDate)
            .NotEmpty().WithMessage("تاريخ الدفع مطلوب");
    }
}

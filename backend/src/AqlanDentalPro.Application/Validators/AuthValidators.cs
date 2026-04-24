using AqlanDentalPro.Application.DTOs.Auth;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب")
            .MaximumLength(50).WithMessage("اسم المستخدم يجب ألا يتجاوز 50 حرفاً");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة");
    }
}

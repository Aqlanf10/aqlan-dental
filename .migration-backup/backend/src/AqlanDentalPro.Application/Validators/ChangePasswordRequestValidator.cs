using AqlanDentalPro.Application.DTOs.Auth;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة");

        // SEC-11: complexity rules are enforced explicitly in AuthController.ChangePassword
        // via PasswordPolicy.Validate so the Arabic error is returned as { message }.
        // The validator only ensures non-empty + differs from current.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
            .NotEqual(x => x.CurrentPassword).WithMessage("كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية");
    }
}

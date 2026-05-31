using AqlanDentalPro.Application.DTOs.Auth;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل")
            .Matches(@"[A-Z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل")
            .Matches(@"[a-z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل")
            .Matches(@"[0-9]").WithMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل")
            .Matches(@"[^A-Za-z0-9]").WithMessage("كلمة المرور يجب أن تحتوي على رمز خاص واحد على الأقل")
            .NotEqual(x => x.CurrentPassword).WithMessage("كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية");
    }
}

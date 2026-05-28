using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Filters;

/// <summary>
/// فلتر إلزام الوردية — يمنع أي عملية دفع (POST /api/finance-v3/payments)
/// إذا لم يكن لدى الكاشير وردية مفتوحة حالياً.
/// المشرفون (Admin) يتجاوزون هذا الفحص كحالة طوارئ.
/// </summary>
public class ShiftEnforcerFilter : IAsyncActionFilter
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ShiftEnforcerFilter(AppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsAdmin;

        // فقط على طلبات POST لمسار المدفوعات
        if (context.HttpContext.Request.Method == "POST" &&
            context.HttpContext.Request.Path.Value?.Contains("/api/finance-v3/payments") == true)
        {
            // المشرفون يتجاوزون فحص الوردية (حالة طوارئ)
            if (!isAdmin)
            {
                if (userId == null || userId == Guid.Empty)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        ErrorCode = "NO_ACTIVE_SHIFT",
                        Message = "لم يتم التعرف على هوية المستخدم. يرجى إعادة تسجيل الدخول."
                    });
                    return;
                }

                // التحقق من وجود وردية مفتوحة لنفس المستخدم في نفس الفرع
                var hasActiveShift = await _context.CashierSessions
                    .AnyAsync(s => s.CashierId == userId.Value && s.Status == SessionStatus.Open && s.IsActive);

                if (!hasActiveShift)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        ErrorCode = "NO_ACTIVE_SHIFT",
                        Message = "لا توجد وردية صندوق مفتوحة. يجب فتح وردية كاشير قبل تسجيل أي مدفوعات نقدية."
                    });
                    return;
                }
            }
        }

        await next();
    }
}

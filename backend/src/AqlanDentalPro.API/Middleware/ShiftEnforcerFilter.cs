using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// فلتر التحقق التلقائي للتحقق من وجود وردية مفتوحة للمستخدم
/// قبل السماح بعمليات الدفع بالخزينة.
///
/// القاعدة: أي عملية POST على مسار الدفعات (/api/finance-v3/payments)
/// تتطلب وردية كاشير مفتوحة ونشطة للمستخدم الحالي.
/// الاستثناء: المدير (Admin) يتجاوز هذا الفحص للطوارئ.
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
        // 1. استخراج هوية الصراف أو المستخدم الحالي
        var userId = _currentUserService.UserId;
        var role = _currentUserService.Role;

        // 2. إذا كان المدخل عملية كتابة دفع (POST) وتستهدف مسار الدفعات
        if (context.HttpContext.Request.Method == "POST" &&
            context.HttpContext.Request.Path.Value?.Contains("/api/finance-v3/payments") == true)
        {
            // تغاضي جزئي للمدير الإداري الأعلى فقط في الطوارئ
            if (role != UserRole.Admin)
            {
                // 3. التحقق من وجود وردية مفتوحة ونشطة للمستخدم الحالي
                var hasActiveShift = await _context.CashierSessions
                    .AnyAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

                if (!hasActiveShift)
                {
                    // الحظر المبكر وإرجاع الخطأ 400 BadRequest مع رسالة تنبيهية
                    context.Result = new BadRequestObjectResult(new
                    {
                        ErrorCode = "NO_ACTIVE_SHIFT",
                        Message = "خطأ حرج: لا توجد وردية مفتوحة حالياً لهذا الصراف بالخزينة. يرجى فتح وردية أولاً للتمكن من استلام المبالغ والتحصيلات المزدوجة."
                    });
                    return;
                }
            }
        }

        await next();
    }
}

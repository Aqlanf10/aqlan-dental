using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AqlanDentalPro.API.Attributes;

/// <summary>
/// Validates that honeypot fields are empty.
/// Bots typically fill all form fields including hidden honeypot fields.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HoneypotAttribute : ActionFilterAttribute
{
    public string FieldName { get; set; } = "website_url";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Check form data, query string, and JSON body
        if (context.HttpContext.Request.HasFormContentType)
        {
            if (context.HttpContext.Request.Form.TryGetValue(FieldName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                context.Result = new OkResult(); // Return 200 silently to bot
                return;
            }
        }

        // For JSON body, check the route value or action arguments
        if (context.ActionArguments.TryGetValue(FieldName, out var fieldValue) && 
            fieldValue is string strValue && 
            !string.IsNullOrWhiteSpace(strValue))
        {
            context.Result = new OkResult(); // Return 200 silently to bot
            return;
        }

        base.OnActionExecuting(context);
    }
}

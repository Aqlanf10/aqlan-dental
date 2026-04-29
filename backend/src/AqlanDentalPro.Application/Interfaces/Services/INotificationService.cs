namespace AqlanDentalPro.Application.Interfaces.Services;

public interface INotificationService
{
    Task NotifyUserAsync(Guid userId, string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null);
    Task NotifyRoleAsync(string role, string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null);
    Task NotifyAllAsync(string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null);
}

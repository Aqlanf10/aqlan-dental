namespace AqlanDentalPro.Application.Interfaces.Services;

public interface INotificationService
{
    Task NotifyAsync(Guid userId, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null);

    Task NotifyRoleAsync(string role, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null);

    // Notify the user linked to a Doctor record
    Task NotifyDoctorAsync(Guid doctorId, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null);
}

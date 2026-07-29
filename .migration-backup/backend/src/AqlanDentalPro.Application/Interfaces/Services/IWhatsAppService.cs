using AqlanDentalPro.Application.DTOs.WhatsApp;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IWhatsAppService
{
    Task<WhatsAppMessageDto> SendMessageAsync(SendMessageRequest request);
    Task<List<WhatsAppMessageDto>> SendBulkAsync(BulkSendMessageRequest request);
    Task<WhatsAppMessageDto?> SendAppointmentReminderAsync(SendAppointmentReminderRequest request);
    Task<int> SendPendingRemindersAsync(); // Called by a background job
    Task<WhatsAppDashboardDto> GetDashboardAsync();
    Task<List<WhatsAppMessageDto>> GetMessageHistoryAsync(Guid? patientId = null, int page = 1, int pageSize = 20);
    Task<bool> RetryFailedMessageAsync(Guid messageId);
    Task<List<WhatsAppTemplateDto>> GetTemplatesAsync();
    Task<WhatsAppTemplateDto?> UpdateTemplateAsync(Guid templateId, UpdateWhatsAppTemplateRequest request);
    Task<WhatsAppMessageDto?> GetMessageStatusAsync(Guid messageId);
}

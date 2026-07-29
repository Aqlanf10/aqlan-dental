using AqlanDentalPro.Application.DTOs.Sms;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ISmsService
{
    Task<SmsMessageDto> SendSmsAsync(SendSmsRequest request);
    Task<List<SmsMessageDto>> SendBulkAsync(BulkSendSmsRequest request);
    Task<SmsMessageDto> SendAppointmentReminderAsync(SendSmsAppointmentReminderRequest request);
    Task<int> SendPendingRemindersAsync();
    Task<SmsDashboardDto> GetDashboardAsync();
    Task<PagedResult<SmsMessageDto>> GetMessagesAsync(int page = 1, int pageSize = 20, string? status = null);
    Task<SmsMessageDto> RetryFailedMessageAsync(Guid messageId);
    Task<List<SmsTemplateDto>> GetTemplatesAsync();
    Task<SmsTemplateDto> UpdateTemplateAsync(Guid templateId, string contentTemplate, string? nameAr = null);
    Task<SmsGatewaySettingsDto> GetGatewaySettingsAsync();
    Task<SmsGatewaySettingsDto> UpdateGatewaySettingsAsync(UpdateSmsGatewaySettingsRequest request);
    Task<bool> TestGatewayConnectionAsync();
}

/// <summary>
/// Simple paged result for SMS message queries.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

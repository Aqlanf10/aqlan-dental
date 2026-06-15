using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.API.Hubs;
using AqlanDentalPro.Infrastructure.Repositories;
using AqlanDentalPro.Infrastructure.Services;
using MessagingService = AqlanDentalPro.Infrastructure.Services.MessagingService;
using PatientPortalService = AqlanDentalPro.Infrastructure.Services.PatientPortalService;
using WhatsAppService = AqlanDentalPro.Infrastructure.Services.WhatsAppService;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for application service DI registrations.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class ServiceRegistrationConfiguration
{
    /// <summary>
    /// Registers all application repositories, services, hosted services, and HTTP clients.
    /// </summary>
    public static void AddApplicationServices(this IServiceCollection services)
    {
        // ── DI — Repositories ────────────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();

        // ── DI — Services ────────────────────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<PatientService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<OrthoService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<ITreasuryResolutionService, TreasuryResolutionService>();
        services.AddScoped<GeneralService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IPatientAccountLinkingService, PatientAccountLinkingService>();
        services.AddScoped<IPatientPortalMessagingService, PatientPortalMessagingService>();
        services.AddScoped<IRealTimePushService, SignalRPushService>();
        services.AddScoped<CephService>();
        services.AddScoped<PhotoAnalysisService>();
        services.AddScoped<AqlanDentalPro.API.Services.CephReportPdfGenerator>();
        services.AddScoped<AqlanDentalPro.API.Services.PhotoAnalysisReportPdfGenerator>();
        services.AddScoped<AqlanDentalPro.API.Services.OrthoModelAnalysisReportPdfGenerator>();
        services.AddScoped<AqlanDentalPro.API.Services.OrthoCaseSummaryReportPdfGenerator>();
        services.AddScoped<IPatientPortalService, PatientPortalService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<INotificationService, AqlanDentalPro.Infrastructure.Services.NotificationService>();
        services.AddHostedService<AqlanDentalPro.Infrastructure.Services.OverdueNotificationJob>();
        services.AddHostedService<AqlanDentalPro.Infrastructure.Services.AppointmentReminderJob>();
        services.AddHostedService<AqlanDentalPro.API.Services.AutoBackupJob>();
        services.AddScoped<IBookingRequestService, AqlanDentalPro.Infrastructure.Services.BookingRequestService>();
        services.AddScoped<AqlanDentalPro.Application.Interfaces.Services.ICommissionService, AqlanDentalPro.Infrastructure.Services.CommissionService>();
        services.AddScoped<AqlanDentalPro.Application.Interfaces.Services.IPatientAccessService, AqlanDentalPro.Infrastructure.Services.PatientAccessService>();
        services.AddScoped<ILoginAttemptService, LoginAttemptService>();
        services.AddHttpClient(); // Register IHttpClientFactory for PatientPortalService
        services.AddHttpClient("RemoteClinicalImage", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AqlanDentalPro/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        });
        services.AddHttpClient<IRecaptchaService, RecaptchaService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpClient("WhatsApp", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        // Ceph batch C-D — real LLM draft-diagnosis assistant. The orchestrator
        // (CephAiDraftService) resolves a provider from the IAiDraftProvider set
        // (gemini default, anthropic; openai recognized but not implemented).
        // Named client follows the WhatsApp/Sms pattern; 60s because LLM
        // generation is slower than messaging webhooks.
        services.AddScoped<CephAiDraftService>();
        services.AddScoped<CephAiLandmarkDraftService>();
        services.AddScoped<AiApiKeyVault>();
        services.AddScoped<IAiDraftProvider, AqlanDentalPro.Infrastructure.Services.Ai.GeminiAiDraftProvider>();
        services.AddScoped<IAiDraftProvider, AqlanDentalPro.Infrastructure.Services.Ai.AnthropicAiDraftProvider>();
        services.AddScoped<ICephLandmarkDraftProvider, AqlanDentalPro.Infrastructure.Services.Ai.GeminiCephLandmarkDraftProvider>();
        services.AddHttpClient(CephAiDraftService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<ISmsService, SmsService>();
        services.AddHttpClient("Sms", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpContextAccessor();
    }
}

using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace AqlanDentalPro.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalHistory> MedicalHistories => Set<MedicalHistory>();
    public DbSet<DentalHistory> DentalHistories => Set<DentalHistory>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<OrthoCase> OrthoCases => Set<OrthoCase>();
    public DbSet<OrthoClinicalExam> OrthoClinicalExams => Set<OrthoClinicalExam>();
    public DbSet<ProblemListItem> ProblemListItems => Set<ProblemListItem>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<OrthoVisit> OrthoVisits => Set<OrthoVisit>();
    public DbSet<TreatmentStage> TreatmentStages => Set<TreatmentStage>();
    public DbSet<RetentionRecord> RetentionRecords => Set<RetentionRecord>();
    public DbSet<RetentionVisit> RetentionVisits => Set<RetentionVisit>();
    public DbSet<CephAnalysis> CephAnalyses => Set<CephAnalysis>();
    public DbSet<CephLandmark> CephLandmarks => Set<CephLandmark>();
    public DbSet<CephMeasurement> CephMeasurements => Set<CephMeasurement>();
    public DbSet<CephDiagnosis> CephDiagnoses => Set<CephDiagnosis>();
    public DbSet<ModelAnalysis> ModelAnalyses => Set<ModelAnalysis>();
    public DbSet<ExtractionDecision> ExtractionDecisions => Set<ExtractionDecision>();
    public DbSet<DentalChart> DentalCharts => Set<DentalChart>();
    public DbSet<ToothCondition> ToothConditions => Set<ToothCondition>();
    public DbSet<GeneralTreatment> GeneralTreatments => Set<GeneralTreatment>();
    public DbSet<PerioAssessment> PerioAssessments => Set<PerioAssessment>();
    public DbSet<SurgeryCase> SurgeryCases => Set<SurgeryCase>();
    public DbSet<PreopReport> PreopReports => Set<PreopReport>();
    public DbSet<OperativeReport> OperativeReports => Set<OperativeReport>();
    public DbSet<PostopRecord> PostopRecords => Set<PostopRecord>();
    public DbSet<HospitalReferral> HospitalReferrals => Set<HospitalReferral>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ClinicalPhoto> ClinicalPhotos => Set<ClinicalPhoto>();
    public DbSet<Radiograph> Radiographs => Set<Radiograph>();
    public DbSet<InternalReferral> InternalReferrals => Set<InternalReferral>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Inventory> Inventory => Set<Inventory>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRead> MessageReads => Set<MessageRead>();
    public DbSet<PatientAccount> PatientAccounts => Set<PatientAccount>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<PerioRecord> PerioRecords => Set<PerioRecord>();
    public DbSet<GeneralTreatmentPlanItem> GeneralTreatmentPlanItems => Set<GeneralTreatmentPlanItem>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global soft-delete query filter for all ISoftDeletable entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsActive));
                var filter = Expression.Lambda(property, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}

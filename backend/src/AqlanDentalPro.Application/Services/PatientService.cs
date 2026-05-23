using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Application.Services;

public class PatientService(
    IPatientRepository repo,
    ICurrentUserService currentUser,
    IConfiguration config,
    IPatientPortalService portalService,
    ILogger<PatientService> logger)
{
    private string NumberPrefix => config["Settings:PatientNumberPrefix"] ?? "GM";

    public async Task<PaginatedResponse<PatientListDto>> GetListAsync(
        string? search, int page, int pageSize, string? gender = null, Guid? doctorId = null,
        string? status = "active", IReadOnlySet<Guid>? allowedPatientIds = null)
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var result = await repo.SearchAsync(search, page, pageSize, branchId, gender, doctorId, status, allowedPatientIds);

        // Batch-load last visit/appointment dates for the current page
        var patientIds = result.Data.Select(p => p.Id).ToList();
        var lastVisitDates = patientIds.Count > 0
            ? await repo.GetLastVisitDatesAsync(patientIds)
            : new Dictionary<Guid, DateTime?>();

        return new PaginatedResponse<PatientListDto>
        {
            Data = result.Data.Select(p =>
            {
                var dto = ToListDto(p);
                dto.LastVisitDate = lastVisitDates.GetValueOrDefault(p.Id);
                return dto;
            }),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PatientProfileDto?> GetByIdAsync(Guid id)
    {
        var patient = await repo.GetWithHistoriesAsync(id);
        // If not found (may be archived and filtered by global filter), try ignoring filters
        if (patient == null)
            patient = await repo.GetWithHistoriesIgnoreFiltersAsync(id);
        return patient == null ? null : ToProfileDto(patient);
    }

    public async Task<PatientProfileDto> CreateAsync(CreatePatientRequest req)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(req.Phone);
        var normalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);

        // Retry up to 5 times to handle the rare concurrent-insert race on PatientNumber
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var number = await repo.GeneratePatientNumberAsync(NumberPrefix);

            var patient = new Patient
            {
                PatientNumber = number,
                FirstName = req.FirstName,
                MiddleName = req.MiddleName,
                LastName = req.LastName,
                DateOfBirth = req.DateOfBirth != null ? DateOnly.Parse(req.DateOfBirth) : null,
                Gender = req.Gender != null ? Enum.Parse<Gender>(req.Gender, true) : null,
                Phone = req.Phone,
                NormalizedPhone = normalizedPhone,
                WhatsApp = req.WhatsApp,
                NormalizedWhatsApp = normalizedWhatsApp,
                Address = req.Address,
                Occupation = req.Occupation,
                ReferralSource = req.ReferralSource,
                PrimaryDoctorId = req.PrimaryDoctorId,
                BranchId = currentUser.BranchId
            };

            if (req.MedicalHistory != null)
            {
                patient.MedicalHistory = new MedicalHistory
                {
                    ChronicDiseases = req.MedicalHistory.ChronicDiseases,
                    CurrentMedications = req.MedicalHistory.CurrentMedications,
                    DrugAllergies = req.MedicalHistory.DrugAllergies,
                    BleedingDisorders = req.MedicalHistory.BleedingDisorders,
                    IsPregnant = req.MedicalHistory.IsPregnant,
                    TmjProblems = req.MedicalHistory.TmjProblems,
                    PreviousSurgeries = req.MedicalHistory.PreviousSurgeries,
                    Notes = req.MedicalHistory.Notes
                };
            }

            if (req.DentalHistory != null)
            {
                patient.DentalHistory = new DentalHistory
                {
                    ChiefComplaint = req.DentalHistory.ChiefComplaint,
                    PreviousTreatments = req.DentalHistory.PreviousTreatments,
                    MouthBreathing = req.DentalHistory.MouthBreathing,
                    Bruxism = req.DentalHistory.Bruxism,
                    ThumbSucking = req.DentalHistory.ThumbSucking,
                    TongueThrusing = req.DentalHistory.TongueThrusing,
                    Notes = req.DentalHistory.Notes
                };
            }

            try
            {
                await repo.AddAsync(patient);
                await repo.SaveChangesAsync();

                // Auto-create patient portal account and capture credentials
                string? portalUsername = null;
                string? portalPassword = null;
                try
                {
                    var (username, plainPassword) = await portalService.EnsurePatientAccountAsync(patient.Id, patient.PatientNumber, patient.Phone);
                    portalUsername = username;
                    portalPassword = plainPassword;
                }
                catch (Exception ex)
                {
                    // Don't fail patient creation if portal account creation fails
                    logger.LogWarning(ex, "[PatientService] Auto-create portal account failed for patient {PatientNumber}", patient.PatientNumber);
                }

                // Set email on the linked User record if provided
                if (!string.IsNullOrWhiteSpace(req.Email))
                {
                    try
                    {
                        await portalService.SetPatientEmailAsync(patient.Id, req.Email.Trim());
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[PatientService] Failed to set email for patient {PatientNumber}", patient.PatientNumber);
                    }
                }

                var result = await GetByIdAsync(patient.Id) ?? ToProfileDto(patient);

                // Attach portal credentials to the result (temp password shown only once)
                if (portalUsername != null)
                {
                    result.PortalUsername = portalUsername;
                    if (!string.IsNullOrEmpty(portalPassword))
                    {
                        result.PortalTemporaryPassword = portalPassword;
                    }
                }

                return result;
            }
            catch (Exception ex) when (IsPatientNumberConflict(ex))
            {
                // Another request grabbed the same number — detach and retry
                repo.Detach(patient);
            }
        }

        throw new InvalidOperationException("تعذّر إنشاء رقم مريض فريد بعد عدة محاولات.");
    }

    private static bool IsPatientNumberConflict(Exception ex)
    {
        var msg = (ex.InnerException?.Message ?? ex.Message).ToLowerInvariant();
        return (msg.Contains("unique") || msg.Contains("duplicate")) &&
               msg.Contains("patientnumber");
    }

    public async Task<PatientProfileDto?> UpdateAsync(Guid id, UpdatePatientRequest req)
    {
        // Use ignore-filters variant to also find soft-deleted history rows
        var patient = await repo.GetWithHistoriesIgnoreFiltersAsync(id);
        if (patient == null || !patient.IsActive) return null;

        // Normalize and check duplicates before updating
        var normalizedPhone = PhoneNormalizer.Normalize(req.Phone);
        var normalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);

        if (normalizedPhone != null)
        {
            var existingPhone = await repo.FirstOrDefaultAsync(p => 
                p.Id != id && (p.NormalizedPhone == normalizedPhone || p.NormalizedWhatsApp == normalizedPhone) && p.IsActive);
            if (existingPhone != null)
                throw new InvalidOperationException($"رقم الهاتف أو الواتساب مستخدم مسبقاً لمريض آخر: {existingPhone.FirstName} {existingPhone.LastName} (ملف رقم {existingPhone.PatientNumber})");
        }

        if (normalizedWhatsApp != null)
        {
            var existingWA = await repo.FirstOrDefaultAsync(p => 
                p.Id != id && (p.NormalizedWhatsApp == normalizedWhatsApp || p.NormalizedPhone == normalizedWhatsApp) && p.IsActive);
            if (existingWA != null)
                throw new InvalidOperationException($"رقم واتساب أو الهاتف مستخدم مسبقاً لمريض آخر: {existingWA.FirstName} {existingWA.LastName} (ملف رقم {existingWA.PatientNumber})");
        }

        patient.FirstName = req.FirstName;
        patient.MiddleName = req.MiddleName;
        patient.LastName = req.LastName;
        patient.DateOfBirth = req.DateOfBirth != null ? DateOnly.Parse(req.DateOfBirth) : null;
        patient.Gender = req.Gender != null ? Enum.Parse<Gender>(req.Gender, true) : null;
        patient.Phone = req.Phone;
        patient.NormalizedPhone = PhoneNormalizer.Normalize(req.Phone);
        patient.WhatsApp = req.WhatsApp;
        patient.NormalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);
        patient.Address = req.Address;
        patient.Occupation = req.Occupation;
        patient.ReferralSource = req.ReferralSource;
        patient.PrimaryDoctorId = req.PrimaryDoctorId;

        if (req.MedicalHistory != null)
        {
            if (patient.MedicalHistory == null)
            {
                // No existing row — create and ADD (not Update) to avoid DbUpdateConcurrencyException
                patient.MedicalHistory = new MedicalHistory
                {
                    PatientId = patient.Id,
                    ChronicDiseases = req.MedicalHistory.ChronicDiseases,
                    CurrentMedications = req.MedicalHistory.CurrentMedications,
                    DrugAllergies = req.MedicalHistory.DrugAllergies,
                    BleedingDisorders = req.MedicalHistory.BleedingDisorders,
                    IsPregnant = req.MedicalHistory.IsPregnant,
                    TmjProblems = req.MedicalHistory.TmjProblems,
                    PreviousSurgeries = req.MedicalHistory.PreviousSurgeries,
                    Notes = req.MedicalHistory.Notes
                };
                await repo.AddChildAsync(patient.MedicalHistory);
            }
            else
            {
                patient.MedicalHistory.ChronicDiseases = req.MedicalHistory.ChronicDiseases;
                patient.MedicalHistory.CurrentMedications = req.MedicalHistory.CurrentMedications;
                patient.MedicalHistory.DrugAllergies = req.MedicalHistory.DrugAllergies;
                patient.MedicalHistory.BleedingDisorders = req.MedicalHistory.BleedingDisorders;
                patient.MedicalHistory.IsPregnant = req.MedicalHistory.IsPregnant;
                patient.MedicalHistory.TmjProblems = req.MedicalHistory.TmjProblems;
                patient.MedicalHistory.PreviousSurgeries = req.MedicalHistory.PreviousSurgeries;
                patient.MedicalHistory.Notes = req.MedicalHistory.Notes;
                // Restore if the row was soft-deleted
                if (!patient.MedicalHistory.IsActive)
                {
                    patient.MedicalHistory.IsActive = true;
                    patient.MedicalHistory.DeletedAt = null;
                    patient.MedicalHistory.DeletedBy = null;
                }
            }
        }

        if (req.DentalHistory != null)
        {
            if (patient.DentalHistory == null)
            {
                // No existing row — create and ADD (not Update) to avoid DbUpdateConcurrencyException
                patient.DentalHistory = new DentalHistory
                {
                    PatientId = patient.Id,
                    ChiefComplaint = req.DentalHistory.ChiefComplaint,
                    PreviousTreatments = req.DentalHistory.PreviousTreatments,
                    MouthBreathing = req.DentalHistory.MouthBreathing,
                    Bruxism = req.DentalHistory.Bruxism,
                    ThumbSucking = req.DentalHistory.ThumbSucking,
                    TongueThrusing = req.DentalHistory.TongueThrusing,
                    Notes = req.DentalHistory.Notes
                };
                await repo.AddChildAsync(patient.DentalHistory);
            }
            else
            {
                patient.DentalHistory.ChiefComplaint = req.DentalHistory.ChiefComplaint;
                patient.DentalHistory.PreviousTreatments = req.DentalHistory.PreviousTreatments;
                patient.DentalHistory.MouthBreathing = req.DentalHistory.MouthBreathing;
                patient.DentalHistory.Bruxism = req.DentalHistory.Bruxism;
                patient.DentalHistory.ThumbSucking = req.DentalHistory.ThumbSucking;
                patient.DentalHistory.TongueThrusing = req.DentalHistory.TongueThrusing;
                patient.DentalHistory.Notes = req.DentalHistory.Notes;
                // Restore if the row was soft-deleted
                if (!patient.DentalHistory.IsActive)
                {
                    patient.DentalHistory.IsActive = true;
                    patient.DentalHistory.DeletedAt = null;
                    patient.DentalHistory.DeletedBy = null;
                }
            }
        }

        // Don't use repo.Update(patient) here — it would mark the entire entity graph as Modified,
        // overriding the Added state of newly created child entities. Since the patient is already
        // tracked by the change tracker (loaded via GetWithHistoriesAsync), property changes above
        // are automatically detected. The new children were explicitly Added via AddChild.
        // Set email on the linked User record if provided
        if (req.Email != null)
        {
            try
            {
                await portalService.SetPatientEmailAsync(id, req.Email);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[PatientService] Failed to set email for patient {PatientId}", id);
            }
        }

        patient.UpdatedAt = DateTime.UtcNow;
        await repo.SaveChangesAsync();
        return ToProfileDto(patient);
    }

    public async Task<(bool exists, string? patientNumber, string? fullName)> CheckDuplicatePhoneAsync(string? phone, Guid? excludeId = null)
    {
        var normalized = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrEmpty(normalized)) return (false, null, null);
        var existing = await repo.FindByNormalizedPhoneAsync(normalized, excludeId);
        if (existing == null) return (false, null, null);
        return (true, existing.PatientNumber, $"{existing.FirstName} {existing.LastName}".Trim());
    }

    public async Task<bool> ArchiveAsync(Guid id)
    {
        var patient = await repo.GetByIdAsync(id);
        if (patient == null) return false;

        patient.IsActive = false;
        patient.UpdatedAt = DateTime.UtcNow;
        repo.Update(patient);
        await repo.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(Guid id)
    {
        var patient = await repo.GetArchivedByIdAsync(id);
        if (patient == null) return false;

        patient.IsActive = true;
        patient.UpdatedAt = DateTime.UtcNow;
        repo.Update(patient);
        await repo.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Upsert medical history: create if none exists, update if it does.
    /// Also handles soft-deleted history rows by restoring them.
    /// Fixes DbUpdateConcurrencyException when patient has no MedicalHistory row.
    /// </summary>
    public async Task<MedicalHistoryDto?> UpsertMedicalHistoryAsync(Guid id, MedicalHistoryDto dto)
    {
        // Use ignore-filters variant to also find soft-deleted history rows
        var patient = await repo.GetWithHistoriesIgnoreFiltersAsync(id);
        if (patient == null || !patient.IsActive) return null;

        if (patient.MedicalHistory == null)
        {
            // No existing row at all — create a NEW entity and ADD it
            patient.MedicalHistory = new MedicalHistory
            {
                PatientId = patient.Id,
                ChronicDiseases = dto.ChronicDiseases,
                CurrentMedications = dto.CurrentMedications,
                DrugAllergies = dto.DrugAllergies,
                BleedingDisorders = dto.BleedingDisorders,
                IsPregnant = dto.IsPregnant,
                TmjProblems = dto.TmjProblems,
                PreviousSurgeries = dto.PreviousSurgeries,
                Notes = dto.Notes
            };
            await repo.AddChildAsync(patient.MedicalHistory);
        }
        else
        {
            // Existing row (active or soft-deleted) — update and restore if needed
            patient.MedicalHistory.ChronicDiseases = dto.ChronicDiseases;
            patient.MedicalHistory.CurrentMedications = dto.CurrentMedications;
            patient.MedicalHistory.DrugAllergies = dto.DrugAllergies;
            patient.MedicalHistory.BleedingDisorders = dto.BleedingDisorders;
            patient.MedicalHistory.IsPregnant = dto.IsPregnant;
            patient.MedicalHistory.TmjProblems = dto.TmjProblems;
            patient.MedicalHistory.PreviousSurgeries = dto.PreviousSurgeries;
            patient.MedicalHistory.Notes = dto.Notes;
            patient.MedicalHistory.UpdatedAt = DateTime.UtcNow;
            // Restore if the row was soft-deleted
            if (!patient.MedicalHistory.IsActive)
            {
                patient.MedicalHistory.IsActive = true;
                patient.MedicalHistory.DeletedAt = null;
                patient.MedicalHistory.DeletedBy = null;
            }
        }

        await repo.SaveChangesAsync();
        return new MedicalHistoryDto
        {
            ChronicDiseases = patient.MedicalHistory.ChronicDiseases,
            CurrentMedications = patient.MedicalHistory.CurrentMedications,
            DrugAllergies = patient.MedicalHistory.DrugAllergies,
            BleedingDisorders = patient.MedicalHistory.BleedingDisorders,
            IsPregnant = patient.MedicalHistory.IsPregnant,
            TmjProblems = patient.MedicalHistory.TmjProblems,
            PreviousSurgeries = patient.MedicalHistory.PreviousSurgeries,
            Notes = patient.MedicalHistory.Notes
        };
    }

    /// <summary>
    /// Upsert dental history: create if none exists, update if it does.
    /// Also handles soft-deleted history rows by restoring them.
    /// Fixes DbUpdateConcurrencyException when patient has no DentalHistory row.
    /// </summary>
    public async Task<DentalHistoryDto?> UpsertDentalHistoryAsync(Guid id, DentalHistoryDto dto)
    {
        // Use ignore-filters variant to also find soft-deleted history rows
        var patient = await repo.GetWithHistoriesIgnoreFiltersAsync(id);
        if (patient == null || !patient.IsActive) return null;

        if (patient.DentalHistory == null)
        {
            // No existing row at all — create a NEW entity and ADD it
            patient.DentalHistory = new DentalHistory
            {
                PatientId = patient.Id,
                ChiefComplaint = dto.ChiefComplaint,
                PreviousTreatments = dto.PreviousTreatments,
                MouthBreathing = dto.MouthBreathing,
                Bruxism = dto.Bruxism,
                ThumbSucking = dto.ThumbSucking,
                TongueThrusing = dto.TongueThrusing,
                Notes = dto.Notes
            };
            await repo.AddChildAsync(patient.DentalHistory);
        }
        else
        {
            // Existing row (active or soft-deleted) — update and restore if needed
            patient.DentalHistory.ChiefComplaint = dto.ChiefComplaint;
            patient.DentalHistory.PreviousTreatments = dto.PreviousTreatments;
            patient.DentalHistory.MouthBreathing = dto.MouthBreathing;
            patient.DentalHistory.Bruxism = dto.Bruxism;
            patient.DentalHistory.ThumbSucking = dto.ThumbSucking;
            patient.DentalHistory.TongueThrusing = dto.TongueThrusing;
            patient.DentalHistory.Notes = dto.Notes;
            patient.DentalHistory.UpdatedAt = DateTime.UtcNow;
            // Restore if the row was soft-deleted
            if (!patient.DentalHistory.IsActive)
            {
                patient.DentalHistory.IsActive = true;
                patient.DentalHistory.DeletedAt = null;
                patient.DentalHistory.DeletedBy = null;
            }
        }

        await repo.SaveChangesAsync();
        return new DentalHistoryDto
        {
            ChiefComplaint = patient.DentalHistory.ChiefComplaint,
            PreviousTreatments = patient.DentalHistory.PreviousTreatments,
            MouthBreathing = patient.DentalHistory.MouthBreathing,
            Bruxism = patient.DentalHistory.Bruxism,
            ThumbSucking = patient.DentalHistory.ThumbSucking,
            TongueThrusing = patient.DentalHistory.TongueThrusing,
            Notes = patient.DentalHistory.Notes
        };
    }

    public async Task<bool> SoftDeleteAsync(Guid id) => await ArchiveAsync(id);

    // LastVisitDate is populated separately by batch-loading appointment/visit dates
    private static PatientListDto ToListDto(Patient p) => new()
    {
        Id = p.Id,
        PatientNumber = p.PatientNumber,
        FullName = $"{p.FirstName} {p.MiddleName} {p.LastName}".Replace("  ", " ").Trim(),
        Phone = p.Phone,
        Gender = p.Gender?.ToString(),
        Age = p.DateOfBirth.HasValue
            ? DateTime.Today.Year - p.DateOfBirth.Value.Year
            : null,
        PrimaryDoctorName = p.PrimaryDoctor?.Name,
        BranchName = p.Branch?.Name,
        CreatedAt = p.CreatedAt,
        IsActive = p.IsActive
    };

    private static PatientProfileDto ToProfileDto(Patient p) => new()
    {
        Id = p.Id,
        PatientNumber = p.PatientNumber,
        FirstName = p.FirstName,
        MiddleName = p.MiddleName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth?.ToString("yyyy-MM-dd"),
        Gender = p.Gender?.ToString(),
        Age = p.DateOfBirth.HasValue ? DateTime.Today.Year - p.DateOfBirth.Value.Year : null,
        Phone = p.Phone,
        WhatsApp = p.WhatsApp,
        Address = p.Address,
        Occupation = p.Occupation,
        ReferralSource = p.ReferralSource,
        PrimaryDoctorId = p.PrimaryDoctorId,
        PrimaryDoctorName = p.PrimaryDoctor?.Name,
        BranchId = p.BranchId,
        BranchName = p.Branch?.Name,
        CreatedAt = p.CreatedAt,
        IsActive = p.IsActive,
        MedicalHistory = p.MedicalHistory == null ? null : new MedicalHistoryDto
        {
            ChronicDiseases = p.MedicalHistory.ChronicDiseases,
            CurrentMedications = p.MedicalHistory.CurrentMedications,
            DrugAllergies = p.MedicalHistory.DrugAllergies,
            BleedingDisorders = p.MedicalHistory.BleedingDisorders,
            IsPregnant = p.MedicalHistory.IsPregnant,
            TmjProblems = p.MedicalHistory.TmjProblems,
            PreviousSurgeries = p.MedicalHistory.PreviousSurgeries,
            Notes = p.MedicalHistory.Notes
        },
        DentalHistory = p.DentalHistory == null ? null : new DentalHistoryDto
        {
            ChiefComplaint = p.DentalHistory.ChiefComplaint,
            PreviousTreatments = p.DentalHistory.PreviousTreatments,
            MouthBreathing = p.DentalHistory.MouthBreathing,
            Bruxism = p.DentalHistory.Bruxism,
            ThumbSucking = p.DentalHistory.ThumbSucking,
            TongueThrusing = p.DentalHistory.TongueThrusing,
            Notes = p.DentalHistory.Notes
        }
    };
}

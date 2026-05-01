using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Utilities;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace AqlanDentalPro.Application.Services;

public class PatientService(
    IPatientRepository repo,
    ICurrentUserService currentUser,
    IConfiguration config)
{
    private string NumberPrefix => config["Settings:PatientNumberPrefix"] ?? "GM";

    public async Task<PaginatedResponse<PatientListDto>> GetListAsync(
        string? search, int page, int pageSize, string? gender = null, Guid? doctorId = null, string? status = "active")
    {
        var branchId = currentUser.IsAdmin ? null : currentUser.BranchId;
        var result = await repo.SearchAsync(search, page, pageSize, branchId, gender, doctorId, status);

        return new PaginatedResponse<PatientListDto>
        {
            Data = result.Data.Select(ToListDto),
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
                return await GetByIdAsync(patient.Id) ?? ToProfileDto(patient);
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
        var patient = await repo.GetWithHistoriesAsync(id);
        if (patient == null) return null;

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
            patient.MedicalHistory ??= new MedicalHistory { PatientId = patient.Id };
            patient.MedicalHistory.ChronicDiseases    = req.MedicalHistory.ChronicDiseases;
            patient.MedicalHistory.CurrentMedications = req.MedicalHistory.CurrentMedications;
            patient.MedicalHistory.DrugAllergies      = req.MedicalHistory.DrugAllergies;
            patient.MedicalHistory.BleedingDisorders  = req.MedicalHistory.BleedingDisorders;
            patient.MedicalHistory.IsPregnant         = req.MedicalHistory.IsPregnant;
            patient.MedicalHistory.TmjProblems        = req.MedicalHistory.TmjProblems;
            patient.MedicalHistory.PreviousSurgeries  = req.MedicalHistory.PreviousSurgeries;
            patient.MedicalHistory.Notes              = req.MedicalHistory.Notes;
        }

        if (req.DentalHistory != null)
        {
            patient.DentalHistory ??= new DentalHistory { PatientId = patient.Id };
            patient.DentalHistory.ChiefComplaint     = req.DentalHistory.ChiefComplaint;
            patient.DentalHistory.PreviousTreatments = req.DentalHistory.PreviousTreatments;
            patient.DentalHistory.MouthBreathing     = req.DentalHistory.MouthBreathing;
            patient.DentalHistory.Bruxism            = req.DentalHistory.Bruxism;
            patient.DentalHistory.ThumbSucking       = req.DentalHistory.ThumbSucking;
            patient.DentalHistory.TongueThrusing     = req.DentalHistory.TongueThrusing;
            patient.DentalHistory.Notes              = req.DentalHistory.Notes;
        }

        repo.Update(patient);
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

    public async Task<bool> SoftDeleteAsync(Guid id) => await ArchiveAsync(id);

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

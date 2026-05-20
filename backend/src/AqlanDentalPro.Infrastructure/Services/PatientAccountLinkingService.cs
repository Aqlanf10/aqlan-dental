using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Unified service for linking PatientAccounts to messaging User entities.
/// This is the single source of truth for the linking logic, replacing the
/// previously duplicated EnsureLinkedUserAsync in PatientPortalService and
/// PatientPortalMessagingService.
/// </summary>
public class PatientAccountLinkingService : IPatientAccountLinkingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PatientAccountLinkingService> _logger;

    public PatientAccountLinkingService(AppDbContext db, ILogger<PatientAccountLinkingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid?> EnsureLinkedUserAsync(Guid patientId)
    {
        var account = await _db.PatientAccounts
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.PatientId == patientId);

        if (account == null) return null;
        if (account.LinkedUserId.HasValue)
            return account.LinkedUserId.Value;

        // Not yet linked — create the link
        var username = account.Username ?? account.Patient?.PatientNumber ?? $"patient-{patientId}";
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (existingUser != null)
        {
            account.LinkedUserId = existingUser.Id;
        }
        else
        {
            var linkedUser = new User
            {
                Username = username,
                PasswordHash = account.PasswordHash ?? "",
                PasswordSalt = account.PasswordSalt ?? "",
                Role = UserRole.Patient,
                IsActive = true
            };
            _db.Users.Add(linkedUser);
            await _db.SaveChangesAsync();
            account.LinkedUserId = linkedUser.Id;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Auto-linked PatientAccount {Username} to messaging User {UserId}",
            account.Username, account.LinkedUserId);

        return account.LinkedUserId.Value;
    }
}

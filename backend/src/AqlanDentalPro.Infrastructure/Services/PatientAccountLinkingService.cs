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
        // FIX: Use IgnoreQueryFilters to find soft-deleted Users by username.
        // Without this, a soft-deleted User with the same username is invisible,
        // causing a unique constraint violation on re-creation.
        var existingUser = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username);

        if (existingUser != null)
        {
            // Reactivate soft-deleted user if needed
            if (!existingUser.IsActive)
            {
                existingUser.IsActive = true;
                existingUser.DeletedAt = null;
                _logger.LogInformation("Reactivated soft-deleted User {Username} for linking", username);
            }
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
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Concurrent request created the user — reload it
                existingUser = await _db.Users.IgnoreQueryFilters()
                    .FirstAsync(u => u.Username == username);
                account.LinkedUserId = existingUser.Id;
                await _db.SaveChangesAsync();
                return account.LinkedUserId.Value;
            }
            account.LinkedUserId = linkedUser.Id;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Auto-linked PatientAccount {Username} to messaging User {UserId}",
            account.Username, account.LinkedUserId);

        return account.LinkedUserId.Value;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505";
    }
}

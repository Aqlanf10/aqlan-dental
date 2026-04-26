using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

public class AuditService(AppDbContext db, ICurrentUserService currentUser) : IAuditService
{
    public async Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
        object? oldData = null, object? newData = null)
    {
        var log = new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            Resource = resource,
            ResourceId = resourceId,
            OldData = oldData != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(oldData)) : null,
            NewData = newData != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(newData)) : null
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}

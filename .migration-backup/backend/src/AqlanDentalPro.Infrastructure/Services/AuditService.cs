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
        await LogAsync(action, resource, resourceId, oldData, newData, details: null);
    }

    public async Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
        object? oldData = null, object? newData = null, string? details = null)
    {
        object? finalNewData = newData;
        if (details != null && newData == null)
        {
            finalNewData = new { _details = details };
        }
        else if (details != null && newData != null)
        {
            // Merge details into newData as a _details property
            var newDataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(newData)) ?? new();
            newDataDict["_details"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(details));
            finalNewData = newDataDict;
        }

        var log = new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            Resource = resource,
            ResourceId = resourceId,
            OldData = oldData != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(oldData)) : null,
            NewData = finalNewData != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(finalNewData)) : null
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}

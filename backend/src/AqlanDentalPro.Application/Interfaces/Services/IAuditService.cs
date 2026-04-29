using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IAuditService
{
    Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
        object? oldData = null, object? newData = null);
}

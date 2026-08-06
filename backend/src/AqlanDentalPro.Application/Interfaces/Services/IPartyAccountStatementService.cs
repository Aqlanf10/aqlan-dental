using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPartyAccountStatementService
{
    Task<PartyAccountStatementDto?> GetAsync(
        string partyType,
        Guid partyId,
        Guid? branchId,
        string? currency,
        CancellationToken cancellationToken = default);
}

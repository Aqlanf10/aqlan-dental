using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IFinanceService
{
    Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status);
    Task<ContractDetailDto?> GetContractByIdAsync(Guid id);
    Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req);
    Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req);
    Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status);
    Task<List<OverdueContractDto>> GetOverdueContractsAsync();
    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);
    Task<PaymentDto?> GetPaymentByIdAsync(Guid id);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req);
    Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req);
    Task<bool> DeletePaymentAsync(Guid id);
    Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason, decimal? partialAmount = null);
    Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId);
    Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId);
    Task<FinanceSummaryDto> GetSummaryAsync();
    Task TryMarkInvoicePaidAsync(Guid invoiceId);
}

using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IFinanceService
{
    Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req);
    Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status);

    // NOTE: PostInvoiceIssuedEntryAsync + ReverseInvoiceIssuedEntryAsync were moved to
    // IInvoiceLedgerService (TD-021 PR A1). Update call sites to inject IInvoiceLedgerService.
    // NOTE: GetAccountStatementAsync, GetSummaryAsync, GetPatientFinanceSummaryAsync, and
    // GetOverdueContractsAsync were moved to IFinanceReadService (TD-021 PR A2). Update
    // call sites to inject IFinanceReadService.
    // NOTE: GetContractsAsync, GetContractByIdAsync, and UpdateContractAsync were moved to
    // IContractService (TD-021 PR A3). Update call sites to inject IContractService.
    // NOTE: The payment cluster (GetPaymentsAsync, GetPaymentByIdAsync, CreatePaymentAsync,
    // UpdatePaymentAsync, DeletePaymentAsync, RefundPaymentAsync, TryMarkInvoicePaidAsync)
    // was moved to IPaymentService, and PaySupplierBillAsync + ProcessRefundAsync were moved
    // to ISupplierRefundService (TD-021 PR A4 slices 2+3). Update call sites accordingly.
    // CreateContractAsync + UpdateContractStatusAsync stay here until the final A4 slice
    // moves them to IContractService.
}

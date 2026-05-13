using System.ComponentModel.DataAnnotations;

namespace AqlanDentalPro.Application.DTOs.Finance;

public class UpdateContractRequest
{
    public string? Specialty { get; set; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "إجمالي العقد يجب أن يكون صفراً أو أكثر")]
    public decimal TotalAmount { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "عدد الأقساط يجب أن يكون صفراً أو أكثر")]
    public int InstallmentsCount { get; set; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "قيمة القسط يجب أن تكون صفراً أو أكثر")]
    public decimal? InstallmentAmount { get; set; }

    public string? StartDate { get; set; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "قيمة الخصم يجب أن تكون صفراً أو أكثر")]
    public decimal DiscountAmount { get; set; }

    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }
}

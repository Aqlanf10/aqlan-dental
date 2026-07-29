using System;

namespace AqlanDentalPro.Application.DTOs.Finance;

public class RecentInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusArabic { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

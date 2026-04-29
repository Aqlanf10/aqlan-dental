namespace AqlanDentalPro.Application.DTOs.Finance;

public class FinanceBySpecialtyDto
{
    public string Specialty { get; set; } = string.Empty;
    public string SpecialtyAr { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public int ContractCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalContractValue { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class FinanceByDoctorDto
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public int PaymentCount { get; set; }
    public int ContractCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalContractValue { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class DailyReportDto
{
    public string Date { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public int PaymentCount { get; set; }
    public int NewContracts { get; set; }
    public decimal NewContractValue { get; set; }
    public int ActiveContracts { get; set; }
    public List<DailyPaymentMethodBreakdown> ByMethod { get; set; } = [];
    public List<DailySpecialtyBreakdown> BySpecialty { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];
}

public class DailyPaymentMethodBreakdown
{
    public string Method { get; set; } = string.Empty;
    public string MethodAr { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class DailySpecialtyBreakdown
{
    public string Specialty { get; set; } = string.Empty;
    public string SpecialtyAr { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class MonthlyReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public int TotalPayments { get; set; }
    public int NewContracts { get; set; }
    public decimal NewContractValue { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public List<MonthlyDailyBreakdown> DailyBreakdown { get; set; } = [];
    public List<DailyPaymentMethodBreakdown> ByMethod { get; set; } = [];
    public List<DailySpecialtyBreakdown> BySpecialty { get; set; } = [];
}

public class MonthlyDailyBreakdown
{
    public string Date { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public int PaymentCount { get; set; }
}

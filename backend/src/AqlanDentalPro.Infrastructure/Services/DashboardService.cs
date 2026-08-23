using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public record DashboardStats(
    int AppointmentsToday,
    int NewPatientsToday,
    int TotalPatients,
    int ActiveOrthoCases,
    int PendingLabOrders,
    int OverdueContractsCount,
    decimal TotalRevenueMTD,
    int QueueWaitingCount,
    int PendingBookingRequestsCount,
    int TodayArrivedCount);

/// <summary>
/// Operational alerts for the dashboard attention widget — the daily pain
/// points of the clinic: lab work piling up, no-shows, patients waiting too
/// long, and tomorrow's unconfirmed appointments.
/// </summary>
public record DashboardAlerts(
    int OverdueLabOrdersCount,
    int MaxLabDaysOverdue,
    int TodayNoShowCount,
    int LongWaitingCount,
    int UnconfirmedTomorrowCount,
    int RecallCandidatesCount);

public class DashboardCharts
{
    public List<DailyRevenue> RevenueByDay { get; set; } = new();
    public List<DailyAppointments> AppointmentsByDay { get; set; } = new();
    public OrthoStatusCounts OrthoByStatus { get; set; } = new();
}

public record DailyRevenue(string Date, decimal Amount);
public record DailyAppointments(string Date, int Count);
public class OrthoStatusCounts
{
    public int Active { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
}

public class DashboardService(AppDbContext db, ICurrentUserService currentUser)
{
    public async Task<DashboardStats> GetStatsAsync(bool includeFinance = true)
    {
        var today = ClinicTimeProvider.ClinicToday();
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var apptQuery = db.Appointments.Where(a => a.AppointmentDate == today);
        if (branchId.HasValue) apptQuery = apptQuery.Where(a => a.BranchId == branchId);

        // "New patients today" is a clinic-day question, so the UTC window is derived from the
        // clinic date rather than from midnight UTC. ToUtcRange already exists for exactly this.
        var (todayStart, todayEnd) = ClinicTimeProvider.ToUtcRange(today);
        var patientQuery = db.Patients.Where(p => p.CreatedAt >= todayStart && p.CreatedAt < todayEnd);
        if (branchId.HasValue) patientQuery = patientQuery.Where(p => p.BranchId == branchId);

        var orthoQuery = db.OrthoCases.ActiveCases();
        if (branchId.HasValue) orthoQuery = orthoQuery.Where(o => o.BranchId == branchId);

        // One definition of "pending", shared with /api/lab-orders/pending-count.
        var labQuery = db.LabOrders.Where(l => LabOrderQueryService.PendingStatuses.Contains(l.Status));
        if (branchId.HasValue) labQuery = labQuery.Where(l => l.Patient != null && l.Patient.BranchId == branchId);

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var revenueQuery = db.Payments.Where(p => p.PaymentDate >= monthStart && p.PaymentDate <= today);
        if (branchId.HasValue) revenueQuery = revenueQuery.Where(p => p.BranchId == branchId);

        var overdueCount = 0;
        if (includeFinance)
        {
            // Overdue: active contracts where expected paid > actual paid.
            // Hidden entirely when finance data is not allowed, because even counts
            // of overdue contracts are financial exposure for clinical-only roles.
            var activeContractsQuery = db.Contracts
                .Include(c => c.Payments)
                .Where(c => c.Status == ContractStatus.Active && c.StartDate.HasValue && c.InstallmentsCount > 0);
            if (branchId.HasValue)
                activeContractsQuery = activeContractsQuery.Where(c => c.Patient.BranchId == branchId);

            var activeContracts = await activeContractsQuery.ToListAsync();

            overdueCount = activeContracts.Count(c =>
            {
                var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12)
                                    + (today.Month - c.StartDate.Value.Month);
                var expectedPaid = c.DownPayment
                    + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
                var actualPaid = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
                return expectedPaid - actualPaid > 0;
            });
        }

        var appointmentsToday  = await apptQuery.CountAsync();
        var newPatientsToday   = await patientQuery.CountAsync();
        var totalPatientsQuery = db.Patients.Where(p => p.IsActive);
        if (branchId.HasValue) totalPatientsQuery = totalPatientsQuery.Where(p => p.BranchId == branchId);
        var totalPatients      = await totalPatientsQuery.CountAsync();
        var activeOrthoCases   = await orthoQuery.CountAsync();
        var pendingLabOrders   = await labQuery.CountAsync();
        var totalRevenueMTD    = includeFinance
            ? await revenueQuery.SumAsync(p => (decimal?)p.Amount) ?? 0
            : 0;

        // Queue waiting count
        var queueWaitingCount = await db.ClinicQueueItems
            .CountAsync(q => q.QueueDate == today
                          && q.Status == ClinicQueueStatus.Waiting
                          && q.IsActive);

        // Pending booking requests count
        var pendingBookingRequestsCount = await db.BookingRequests
            .CountAsync(r => r.Status == BookingRequestStatus.Pending && r.IsActive);

        // Today arrived count (appointments with Arrived/Waiting status today)
        var todayArrivedCount = await db.Appointments
            .CountAsync(a => a.AppointmentDate == today
                          && (a.Status == AppointmentStatus.Arrived || a.Status == AppointmentStatus.Waiting)
                          && a.IsActive);

        return new DashboardStats(
            appointmentsToday, newPatientsToday, totalPatients, activeOrthoCases, pendingLabOrders,
            overdueCount, totalRevenueMTD, queueWaitingCount, pendingBookingRequestsCount, todayArrivedCount);
    }

    /// <summary>
    /// "يحتاج انتباهك" — operational alert counters. Branch-scoped for
    /// non-admin users like the rest of the dashboard.
    /// </summary>
    public async Task<DashboardAlerts> GetAlertsAsync(int longWaitMinutes = 30, int recallWindowDays = 30)
    {
        var today = ClinicTimeProvider.ClinicToday();
        var tomorrow = today.AddDays(1);
        var nowUtc = DateTime.UtcNow;
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // التراكيب المتأخرة — the same definition as /api/lab-orders/overdue, which this
        // comment already claimed while the two used different clocks: that endpoint reads
        // ClinicToday and this read the server's date. On Railway (UTC) they disagreed every
        // night between 00:00 and 03:00 Yemen time, so the dashboard and the lab page showed
        // different counts for the same thing.
        var overdueLabQuery = db.LabOrders.Where(l =>
            l.IsActive
            && l.ExpectedDate != null && l.ExpectedDate < today
            && l.Status != "delivered" && l.Status != "cancelled");
        if (branchId.HasValue)
            overdueLabQuery = overdueLabQuery.Where(l => l.Patient != null && l.Patient.BranchId == branchId);

        var overdueLabCount = await overdueLabQuery.CountAsync();
        var oldestExpected = overdueLabCount == 0
            ? (DateOnly?)null
            : await overdueLabQuery.MinAsync(l => l.ExpectedDate);
        var maxDaysOverdue = oldestExpected.HasValue ? today.DayNumber - oldestExpected.Value.DayNumber : 0;

        // غياب اليوم
        var noShowQuery = db.Appointments.Where(a =>
            a.IsActive && a.AppointmentDate == today && a.Status == AppointmentStatus.NoShow);
        if (branchId.HasValue) noShowQuery = noShowQuery.Where(a => a.BranchId == branchId);
        var todayNoShowCount = await noShowQuery.CountAsync();

        // منتظرون أطول من المسموح
        var waitThreshold = nowUtc.AddMinutes(-longWaitMinutes);
        var longWaitingQuery = db.ClinicQueueItems.Where(q =>
            q.IsActive && q.QueueDate == today
            && q.Status == ClinicQueueStatus.Waiting
            && q.CreatedAt <= waitThreshold);
        if (branchId.HasValue)
            longWaitingQuery = longWaitingQuery.Where(q => q.Patient.BranchId == branchId);
        var longWaitingCount = await longWaitingQuery.CountAsync();

        // مواعيد الغد غير المؤكدة
        var unconfirmedQuery = db.Appointments.Where(a =>
            a.IsActive && a.AppointmentDate == tomorrow && a.Status == AppointmentStatus.Scheduled);
        if (branchId.HasValue) unconfirmedQuery = unconfirmedQuery.Where(a => a.BranchId == branchId);
        var unconfirmedTomorrowCount = await unconfirmedQuery.CountAsync();

        // مرشحون لإعادة الاستدعاء: غابوا خلال النافذة وليس لهم موعد قادم
        var recallSince = today.AddDays(-recallWindowDays);
        var recallQuery = db.Appointments.Where(a =>
            a.IsActive && a.Status == AppointmentStatus.NoShow
            && a.AppointmentDate >= recallSince && a.AppointmentDate <= today
            && !db.Appointments.Any(f =>
                f.IsActive && f.PatientId == a.PatientId
                && f.AppointmentDate >= today
                && f.Status != AppointmentStatus.Cancelled
                && f.Status != AppointmentStatus.NoShow));
        if (branchId.HasValue) recallQuery = recallQuery.Where(a => a.BranchId == branchId);
        var recallCandidatesCount = await recallQuery.Select(a => a.PatientId).Distinct().CountAsync();

        return new DashboardAlerts(
            overdueLabCount, maxDaysOverdue, todayNoShowCount,
            longWaitingCount, unconfirmedTomorrowCount, recallCandidatesCount);
    }

    public async Task<DashboardCharts> GetChartsAsync(bool includeFinance = true)
    {
        var today = ClinicTimeProvider.ClinicToday();
        var thirtyDaysAgo = today.AddDays(-29);

        var chartBranchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // Revenue by day (last 30 days) - fetch raw then format
        var revenueRaw = new List<RevenuePoint>();
        if (includeFinance)
        {
            var revenueQuery = db.Payments
                .Where(p => p.PaymentDate >= thirtyDaysAgo && p.PaymentDate <= today && p.IsActive);
            if (chartBranchId.HasValue) revenueQuery = revenueQuery.Where(p => p.BranchId == chartBranchId);
            revenueRaw = await revenueQuery
                .GroupBy(p => p.PaymentDate)
                .Select(g => new RevenuePoint(g.Key, g.Sum(p => p.Amount)))
                .ToListAsync();
        }

        // Appointments by day (last 30 days) - fetch raw then format
        var apptQuery = db.Appointments
            .Where(a => a.AppointmentDate >= thirtyDaysAgo && a.AppointmentDate <= today && a.IsActive);
        if (chartBranchId.HasValue) apptQuery = apptQuery.Where(a => a.BranchId == chartBranchId);
        var apptRaw = await apptQuery
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new { date = g.Key, count = g.Count() })
            .ToListAsync();

        // Ortho by status
        var orthoChartQuery = db.OrthoCases.AsQueryable();
        if (chartBranchId.HasValue) orthoChartQuery = orthoChartQuery.Where(o => o.BranchId == chartBranchId);
        var orthoGroups = await orthoChartQuery
            .GroupBy(o => o.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        // Fill all 30 days (including zeros)
        var revenueByDay = Enumerable.Range(0, 30)
            .Select(i => today.AddDays(-29 + i))
            .Select(d => new DailyRevenue(
                d.ToString("MM/dd"),
                revenueRaw.FirstOrDefault(r => r.Date == d)?.Amount ?? 0))
            .ToList();

        var apptByDay = Enumerable.Range(0, 30)
            .Select(i => today.AddDays(-29 + i))
            .Select(d => new DailyAppointments(
                d.ToString("MM/dd"),
                apptRaw.FirstOrDefault(r => r.date == d)?.count ?? 0))
            .ToList();

        return new DashboardCharts
        {
            RevenueByDay = revenueByDay,
            AppointmentsByDay = apptByDay,
            OrthoByStatus = new OrthoStatusCounts
            {
                Active    = orthoGroups.FirstOrDefault(g => g.status == OrthoCaseStatus.Active)?.count ?? 0,
                Completed = orthoGroups.FirstOrDefault(g => g.status == OrthoCaseStatus.Completed)?.count ?? 0,
                Cancelled = orthoGroups.FirstOrDefault(g => g.status == OrthoCaseStatus.Cancelled)?.count ?? 0,
            }
        };
    }

    private sealed record RevenuePoint(DateOnly Date, decimal Amount);
}

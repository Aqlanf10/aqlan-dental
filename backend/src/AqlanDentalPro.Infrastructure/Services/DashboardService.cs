using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public record DashboardStats(
    int AppointmentsToday,
    int NewPatientsToday,
    int ActiveOrthoCases,
    int PendingLabOrders);

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
    public async Task<DashboardStats> GetStatsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var apptQuery = db.Appointments.Where(a => a.AppointmentDate == today);
        if (branchId.HasValue) apptQuery = apptQuery.Where(a => a.BranchId == branchId);

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd   = todayStart.AddDays(1);
        var patientQuery = db.Patients.Where(p => p.CreatedAt >= todayStart && p.CreatedAt < todayEnd);
        if (branchId.HasValue) patientQuery = patientQuery.Where(p => p.BranchId == branchId);

        var orthoQuery = db.OrthoCases.Where(o => o.Status == "active");
        if (branchId.HasValue) orthoQuery = orthoQuery.Where(o => o.BranchId == branchId);

        var labQuery = db.LabOrders.Where(l => l.Status == "sent" || l.Status == "manufacturing");

        var appointmentsToday  = await apptQuery.CountAsync();
        var newPatientsToday   = await patientQuery.CountAsync();
        var activeOrthoCases   = await orthoQuery.CountAsync();
        var pendingLabOrders   = await labQuery.CountAsync();

        return new DashboardStats(appointmentsToday, newPatientsToday, activeOrthoCases, pendingLabOrders);
    }

    public async Task<DashboardCharts> GetChartsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var thirtyDaysAgo = today.AddDays(-29);

        // Revenue by day (last 30 days) - fetch raw then format
        var revenueRaw = await db.Payments
            .Where(p => p.PaymentDate >= thirtyDaysAgo && p.PaymentDate <= today)
            .GroupBy(p => p.PaymentDate)
            .Select(g => new { date = g.Key, amount = g.Sum(p => p.Amount) })
            .ToListAsync();

        // Appointments by day (last 30 days) - fetch raw then format
        var apptRaw = await db.Appointments
            .Where(a => a.AppointmentDate >= thirtyDaysAgo && a.AppointmentDate <= today)
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new { date = g.Key, count = g.Count() })
            .ToListAsync();

        // Ortho by status
        var orthoGroups = await db.OrthoCases
            .GroupBy(o => o.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        // Fill all 30 days (including zeros)
        var revenueByDay = Enumerable.Range(0, 30)
            .Select(i => today.AddDays(-29 + i))
            .Select(d => new DailyRevenue(
                d.ToString("MM/dd"),
                revenueRaw.FirstOrDefault(r => r.date == d)?.amount ?? 0))
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
                Active    = orthoGroups.FirstOrDefault(g => g.status == "active")?.count ?? 0,
                Completed = orthoGroups.FirstOrDefault(g => g.status == "completed")?.count ?? 0,
                Cancelled = orthoGroups.FirstOrDefault(g => g.status == "cancelled")?.count ?? 0,
            }
        };
    }
}

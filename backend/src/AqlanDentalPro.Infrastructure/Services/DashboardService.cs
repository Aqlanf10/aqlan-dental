using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public record DashboardStats(
    int AppointmentsToday,
    int NewPatientsToday,
    int ActiveOrthoCases,
    int PendingLabOrders);

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
}

using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using LabEntity = AqlanDentalPro.Domain.Entities.Lab;
using LabOrderEntity = AqlanDentalPro.Domain.Entities.LabOrder;
using LabPayableEntity = AqlanDentalPro.Domain.Entities.LabPayable;
using PatientEntity = AqlanDentalPro.Domain.Entities.Patient;
using GenderEnum = AqlanDentalPro.Domain.Enums.Gender;

namespace AqlanDentalPro.UnitTests.LabSprint6;

/// <summary>
/// Lab Sprint 6 — Unit tests for lab reports and analytics logic.
/// Uses InMemory database to verify KPI calculations.
/// </summary>
public class LabReportsTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LabReportsTest_{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        return db;
    }

    private static PatientEntity CreatePatient(Guid id, string firstName = "أحمد", string lastName = "محمد", string patientNumber = "P-001")
        => new PatientEntity { Id = id, FirstName = firstName, LastName = lastName, PatientNumber = patientNumber, DateOfBirth = new DateOnly(1990, 1, 1), Gender = GenderEnum.Male };

    private static LabEntity CreateLab(Guid id, string name = "مختبر الأسنان")
        => new LabEntity { Id = id, Name = name };

    private static LabOrderEntity CreateLabOrder(
        Guid id,
        Guid patientId,
        string status = "sent",
        Guid? labId = null,
        DateOnly? sentDate = null,
        DateOnly? expectedDate = null,
        DateOnly? receivedDate = null,
        decimal? cost = 100,
        int remakeCount = 0)
        => new()
        {
            Id = id,
            PatientId = patientId,
            Status = status,
            LabId = labId,
            SentDate = sentDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            ExpectedDate = expectedDate,
            ReceivedDate = receivedDate,
            Cost = cost,
            TotalCost = cost,
            RemakeCount = remakeCount,
            OrderNumber = $"LAB-{DateTime.UtcNow.Year}-001",
            ApplianceType = "تاج",
            Priority = "normal",
        };

    [Fact]
    public async Task OverdueOrders_ShouldReturn_OrdersPastExpectedDate()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        var lab = CreateLab(labId);
        db.Patients.Add(patient);
        db.Labs.Add(lab);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Overdue order - expected yesterday, not delivered
        var overdueOrderId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(overdueOrderId, patientId, "sent", labId, expectedDate: today.AddDays(-1)));

        // Not overdue - expected tomorrow
        var futureOrderId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(futureOrderId, patientId, "sent", labId, expectedDate: today.AddDays(1)));

        // Not overdue - delivered (even though past expected date)
        var deliveredOrderId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(deliveredOrderId, patientId, "delivered", labId, expectedDate: today.AddDays(-5)));

        await db.SaveChangesAsync();

        // Act
        var overdueOrders = await db.LabOrders
            .Where(o => o.ExpectedDate != null
                && o.ExpectedDate < today
                && o.Status != "delivered"
                && o.Status != "cancelled")
            .ToListAsync();

        // Assert
        Assert.Single(overdueOrders);
        Assert.Equal(overdueOrderId, overdueOrders[0].Id);
    }

    [Fact]
    public async Task ReadyForDelivery_ShouldReturn_ReceivedOrdersOnly()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        db.Patients.Add(patient);

        var receivedId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(receivedId, patientId, "received"));

        var readyId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(readyId, patientId, "ready"));

        var sentId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(sentId, patientId, "sent"));

        var deliveredId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(deliveredId, patientId, "delivered"));

        await db.SaveChangesAsync();

        // Act
        var readyForDelivery = await db.LabOrders
            .Where(o => o.Status == "received")
            .ToListAsync();

        // Assert
        Assert.Single(readyForDelivery);
        Assert.Equal(receivedId, readyForDelivery[0].Id);
    }

    [Fact]
    public async Task LabPerformance_ShouldCalculate_AvgExecutionDays()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        var lab = CreateLab(labId);
        db.Patients.Add(patient);
        db.Labs.Add(lab);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Order 1: 5 days execution
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", labId,
            sentDate: today.AddDays(-10),
            receivedDate: today.AddDays(-5)));

        // Order 2: 3 days execution
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", labId,
            sentDate: today.AddDays(-8),
            receivedDate: today.AddDays(-5)));

        await db.SaveChangesAsync();

        // Act
        var ordersWithDays = await db.LabOrders
            .Where(o => o.SentDate != null && o.ReceivedDate != null && o.LabId == labId)
            .Select(o => (int)(o.ReceivedDate!.Value.DayNumber - o.SentDate!.Value.DayNumber))
            .ToListAsync();

        var avgDays = ordersWithDays.Count > 0 ? Math.Round(ordersWithDays.Average(), 1) : 0;

        // Assert
        Assert.Equal(4.0, avgDays);
    }

    [Fact]
    public async Task LabPerformance_ShouldCalculate_RemakeRate()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        var lab = CreateLab(labId);
        db.Patients.Add(patient);
        db.Labs.Add(lab);

        // 10 total orders
        for (int i = 0; i < 8; i++)
            db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", labId));

        // 2 remake orders
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "remake", labId, remakeCount: 1));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", labId, remakeCount: 1));

        await db.SaveChangesAsync();

        // Act
        var totalOrders = await db.LabOrders.CountAsync(o => o.LabId == labId);
        var remakeOrders = await db.LabOrders.CountAsync(o => o.LabId == labId && (o.Status == "remake" || o.RemakeCount > 0));
        var remakeRate = Math.Round((double)remakeOrders / totalOrders * 100, 1);

        // Assert
        Assert.Equal(10, totalOrders);
        Assert.Equal(2, remakeOrders);
        Assert.Equal(20.0, remakeRate);
    }

    [Fact]
    public async Task LabCosts_ShouldSum_TotalCostPerLab()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var lab1Id = Guid.NewGuid();
        var lab2Id = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        var lab1 = CreateLab(lab1Id, "المختبر الأول");
        var lab2 = CreateLab(lab2Id, "المختبر الثاني");
        db.Patients.Add(patient);
        db.Labs.AddRange(lab1, lab2);

        // Lab1: 3 orders, total 600
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", lab1Id, cost: 200));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", lab1Id, cost: 150));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent", lab1Id, cost: 250));

        // Lab2: 2 orders, total 500
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", lab2Id, cost: 300));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "manufacturing", lab2Id, cost: 200));

        await db.SaveChangesAsync();

        // Act
        var labCosts = await db.LabOrders
            .Where(o => o.LabId != null)
            .GroupBy(o => new { o.LabId, LabName = o.Lab!.Name })
            .Select(g => new
            {
                LabId = g.Key.LabId,
                LabName = g.Key.LabName,
                TotalCost = g.Sum(o => o.TotalCost ?? o.Cost ?? 0),
                TotalOrders = g.Count(),
            })
            .ToListAsync();

        // Assert
        Assert.Equal(2, labCosts.Count);
        var lab1Report = labCosts.First(l => l.LabId == lab1Id);
        var lab2Report = labCosts.First(l => l.LabId == lab2Id);
        Assert.Equal(600m, lab1Report.TotalCost);
        Assert.Equal(3, lab1Report.TotalOrders);
        Assert.Equal(500m, lab2Report.TotalCost);
        Assert.Equal(2, lab2Report.TotalOrders);
    }

    [Fact]
    public async Task LabDebts_ShouldFilter_UnpaidPayables()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var patient = CreatePatient(patientId);
        var lab = CreateLab(labId);
        db.Patients.Add(patient);
        db.Labs.Add(lab);

        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(order1Id, patientId, "delivered", labId));
        db.LabOrders.Add(CreateLabOrder(order2Id, patientId, "delivered", labId));

        db.LabPayables.AddRange(
            new LabPayableEntity
            {
                LabOrderId = order1Id, LabId = labId,
                Amount = 200, PaidAmount = 0, Status = "pending",
            },
            new LabPayableEntity
            {
                LabOrderId = order2Id, LabId = labId,
                Amount = 300, PaidAmount = 300, Status = "paid",
            });

        await db.SaveChangesAsync();

        // Act
        var unpaidPayables = await db.LabPayables
            .Where(p => p.Status != "paid")
            .ToListAsync();

        var totalDebt = await db.LabPayables
            .Where(p => p.Status != "paid")
            .SumAsync(p => p.Amount - p.PaidAmount);

        // Assert
        Assert.Single(unpaidPayables);
        Assert.Equal(200m, totalDebt);
    }
}

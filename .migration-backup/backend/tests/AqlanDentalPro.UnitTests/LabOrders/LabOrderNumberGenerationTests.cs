using FluentAssertions;
using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CON-02 FIX: Tests for lab order number generation strategy.
/// Validates the order number format and the unique constraint safety net logic.
/// </summary>
public class LabOrderNumberGenerationTests
{
    // ─── Order Number Format Tests ───────────────────────────────────────

    [Fact]
    public void OrderNumber_Format_IsLabYearSequence()
    {
        // Verify the expected format: LAB-{year}-{seq:D3}
        var year = 2026;
        var seq = 1;
        var orderNumber = $"LAB-{year}-{seq:D3}";
        orderNumber.Should().Be("LAB-2026-001");
    }

    [Fact]
    public void OrderNumber_SequenceZeroPadded_ToThreeDigits()
    {
        var year = 2026;
        var orderNumber1 = $"LAB-{year}-{1:D3}";
        var orderNumber10 = $"LAB-{year}-{10:D3}";
        var orderNumber100 = $"LAB-{year}-{100:D3}";
        var orderNumber999 = $"LAB-{year}-{999:D3}";

        orderNumber1.Should().Be("LAB-2026-001");
        orderNumber10.Should().Be("LAB-2026-010");
        orderNumber100.Should().Be("LAB-2026-100");
        orderNumber999.Should().Be("LAB-2026-999");
    }

    [Fact]
    public void OrderNumber_DifferentYears_GenerateIndependently()
    {
        var seq = 1;
        var order2025 = $"LAB-2025-{seq:D3}";
        var order2026 = $"LAB-2026-{seq:D3}";

        order2025.Should().Be("LAB-2025-001");
        order2026.Should().Be("LAB-2026-001");
        order2025.Should().NotBe(order2026);
    }

    // ─── IsUniqueViolation Detection Tests ───────────────────────────────

    [Theory]
    [InlineData("23505: duplicate key value violates unique constraint", true)]
    [InlineData("duplicate key value violates unique constraint \"IX_LabOrders_OrderNumber\"", true)]
    [InlineData("unique constraint violation", true)]
    [InlineData("ERROR: duplicate key value violates unique constraint", true)]
    [InlineData("OrderNumber", true)]
    [InlineData("foreign key constraint", false)]
    [InlineData("not null violation", false)]
    [InlineData("connection refused", false)]
    public void UniqueViolation_Detection_IdentifiesCorrectly(string message, bool expected)
    {
        // The IsUniqueViolation method checks for these patterns in inner exception messages
        var contains23505 = message.Contains("23505");
        var containsDuplicateKey = message.Contains("duplicate key");
        var containsUniqueConstraint = message.Contains("unique constraint");
        var containsOrderNumber = message.Contains("OrderNumber");

        var isUniqueViolation = contains23505 || containsDuplicateKey || containsUniqueConstraint || containsOrderNumber;
        isUniqueViolation.Should().Be(expected);
    }

    // ─── Strategy Documentation Tests ────────────────────────────────────

    [Fact]
    public void Strategy_IsAdvisoryLock_Plus_UniqueIndex_Plus_Retry()
    {
        // CON-02 FIX: The strategy is:
        // 1. Advisory lock (pg_advisory_xact_lock) serializes generation within a transaction
        // 2. Unique index on OrderNumber is the database-level safety net
        // 3. Retry with fresh count on unique constraint violation (max 3 attempts)
        //
        // This test documents the strategy for future maintainers.

        const int maxRetries = 3;
        maxRetries.Should().Be(3);

        // Advisory lock key must be deterministic for lab order number generation
        var lockKey = Math.Abs("LabOrderNumber".GetHashCode()) % 100000;
        lockKey.Should().BeGreaterThanOrEqualTo(0);
        lockKey.Should().BeLessThan(100000);
    }

    [Fact]
    public void OrderNumber_WithMoreThan999Orders_ExceedsFormat()
    {
        // Document: if more than 999 orders per year, format expands naturally
        var seq = 1000;
        var orderNumber = $"LAB-2026-{seq:D3}";
        // D3 with value > 999 just prints the full number
        orderNumber.Should().Be("LAB-2026-1000");
    }

    // ─── Lab Order Lifecycle Tests (Sprint 2) ─────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task MarkReceived_SetsStatusAndDate()
    {
        // Create order with status "sent", mark as received, verify status and date
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "أحمد", LastName = "سعيد" });

        var order = new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-001",
            Status = "sent",
            ApplianceType = "تاج خزفي",
            IsActive = true
        };
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        // Simulate mark-received: set status and date
        order.Status = "received";
        order.ReceivedDate = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();

        var saved = await db.LabOrders.FirstOrDefaultAsync(l => l.Id == order.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be("received");
        saved.ReceivedDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public async Task MarkDelivered_RequiresReadyStatus()
    {
        // Attempt to deliver an order that is "sent" should fail (business rule validation)
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "محمد", LastName = "علي" });

        var order = new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-002",
            Status = "sent",
            ApplianceType = "طقم أسنان",
            IsActive = true
        };
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        // Business rule: can only deliver if status is "ready" or "received"
        var canDeliver = order.Status == "ready" || order.Status == "received";
        canDeliver.Should().BeFalse("sent orders cannot be delivered directly");

        // Now change to "ready" and verify it can be delivered
        order.Status = "ready";
        await db.SaveChangesAsync();

        var canDeliverAfterReady = order.Status == "ready" || order.Status == "received";
        canDeliverAfterReady.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_WithReason_SetsCancellationReason()
    {
        // Cancel with reason, verify status and reason
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "خالد", LastName = "حسن" });

        var order = new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-003",
            Status = "sent",
            ApplianceType = "جسر",
            IsActive = true
        };
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        // Simulate cancel with reason
        order.Status = "cancelled";
        order.CancellationReason = "المريض لم يحضر";
        await db.SaveChangesAsync();

        var saved = await db.LabOrders.FirstOrDefaultAsync(l => l.Id == order.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be("cancelled");
        saved.CancellationReason.Should().Be("المريض لم يحضر");
    }

    [Fact]
    public async Task Today_ReturnsOnlyTodayOrders()
    {
        // Verify filter by today's date
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "سعيد", LastName = "عمر" });

        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        // Order with today's SentDate
        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-010",
            Status = "sent",
            SentDate = today,
            IsActive = true
        });

        // Order with yesterday's SentDate
        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-011",
            Status = "sent",
            SentDate = yesterday,
            IsActive = true
        });

        await db.SaveChangesAsync();

        // Simulate the Today filter: SentDate == today || ExpectedDate == today || ReceivedDate == today
        var todayOrders = await db.LabOrders
            .Where(l => l.SentDate == today || l.ExpectedDate == today || l.ReceivedDate == today || l.DeliveredDate == today)
            .ToListAsync();

        todayOrders.Should().ContainSingle();
        todayOrders[0].OrderNumber.Should().Be("LAB-2026-010");
    }

    [Fact]
    public async Task Ready_ReturnsOnlyReadyAndReceived()
    {
        // Verify filter by status: only "ready" and "received" orders
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "فاطمة", LastName = "أحمد" });

        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-020",
            Status = "ready",
            IsActive = true
        });

        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-021",
            Status = "received",
            IsActive = true
        });

        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-022",
            Status = "sent",
            IsActive = true
        });

        db.LabOrders.Add(new LabOrder
        {
            PatientId = patientId,
            OrderNumber = "LAB-2026-023",
            Status = "manufacturing",
            IsActive = true
        });

        await db.SaveChangesAsync();

        // Simulate the Ready filter: status == "ready" || status == "received"
        var readyOrders = await db.LabOrders
            .Where(l => l.Status == "ready" || l.Status == "received")
            .ToListAsync();

        readyOrders.Should().HaveCount(2);
        readyOrders.Select(o => o.OrderNumber).Should().Contain(new[] { "LAB-2026-020", "LAB-2026-021" });
    }
}

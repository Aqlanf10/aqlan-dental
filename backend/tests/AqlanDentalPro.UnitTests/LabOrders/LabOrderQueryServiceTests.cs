using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LabEntity = AqlanDentalPro.Domain.Entities.Lab;
using LabOrderEntity = AqlanDentalPro.Domain.Entities.LabOrder;
using PatientEntity = AqlanDentalPro.Domain.Entities.Patient;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// Sprint 12 — basic query tests for <see cref="LabOrderQueryService"/>.
///
/// These tests cover the read/query logic that was previously inline in
/// <c>LabOrdersController</c> and had no direct unit coverage (the existing
/// <c>LabReportsTests</c> exercise the EF query patterns in isolation but do
/// not invoke the controller's GET actions). After the Sprint 12 extraction
/// the query logic lives in <see cref="LabOrderQueryService"/> and is testable
/// directly with an in-memory <see cref="AppDbContext"/>.
///
/// Coverage:
///   - GetOverdueAsync: filters out delivered/cancelled; orders by ExpectedDate asc.
///   - GetReadyForDeliveryAsync: only "received" status.
///   - GetPendingCountAsync: counts sent/manufacturing/tryIn/remake only.
///   - GetTodayAsync: includes orders whose SentDate/ExpectedDate/ReceivedDate/DeliveredDate equals today.
///   - GetReadyAsync: includes only "ready" and "received" statuses.
///   - GetByIdAsync: returns null when not found; returns full DTO (with Items) when found.
///   - GetHistoryAsync: returns null when order doesn't exist (404 path).
///   - GetAttachmentsAsync: returns empty list (no existence check) — preserves original behavior.
///   - GetAllAsync: pagination + total count + status filter.
/// </summary>
public class LabOrderQueryServiceTests
{
    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LabOrderQueryServiceTests_{Guid.NewGuid()}")
            .Options);

    private static LabOrderQueryService CreateService(AppDbContext db)
        => new(db, NullLogger<LabOrderQueryService>.Instance);

    private static PatientEntity CreatePatient(Guid id) => new()
    {
        Id = id,
        FirstName = "أحمد",
        LastName = "محمد",
        PatientNumber = "P-001",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Gender = Gender.Male
    };

    private static LabEntity CreateLab(Guid id, string name = "مختبر الأسنان") => new()
    {
        Id = id,
        Name = name
    };

    private static LabOrderEntity CreateLabOrder(
        Guid id,
        Guid patientId,
        string status = "sent",
        Guid? labId = null,
        DateOnly? sentDate = null,
        DateOnly? expectedDate = null,
        DateOnly? receivedDate = null,
        DateOnly? deliveredDate = null,
        decimal? cost = 100,
        int remakeCount = 0) => new()
    {
        Id = id,
        PatientId = patientId,
        Status = status,
        LabId = labId,
        SentDate = sentDate,
        ExpectedDate = expectedDate,
        ReceivedDate = receivedDate,
        DeliveredDate = deliveredDate,
        Cost = cost,
        TotalCost = cost,
        RemakeCount = remakeCount,
        OrderNumber = $"LAB-2026-001",
        ApplianceType = "تاج",
        Priority = "normal",
    };

    // ─── GetOverdueAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverdueAsync_ReturnsOnlyPastExpectedDate_NonDeliveredNonCancelled()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        db.Labs.Add(CreateLab(labId));

        var today = ClinicTimeProvider.ClinicToday();

        // Overdue: expected yesterday, status "sent"
        var overdueId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(overdueId, patientId, "sent", labId, expectedDate: today.AddDays(-1)));

        // Not overdue: expected tomorrow
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent", labId, expectedDate: today.AddDays(1)));

        // Not overdue: delivered (even though past expected date)
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered", labId, expectedDate: today.AddDays(-5)));

        // Not overdue: cancelled (even though past expected date)
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "cancelled", labId, expectedDate: today.AddDays(-5)));

        // Not overdue: no expected date set
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent", labId, expectedDate: null));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetOverdueAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(overdueId);
        result[0].DaysOverdue.Should().Be(1);
        result[0].TotalCost.Should().Be(100m);
    }

    [Fact]
    public async Task GetOverdueAsync_OrdersByExpectedDate_Ascending()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        var today = ClinicTimeProvider.ClinicToday();

        // Two overdue orders, the older one first when ordered ascending by ExpectedDate.
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(olderId, patientId, "sent", expectedDate: today.AddDays(-10)));
        db.LabOrders.Add(CreateLabOrder(newerId, patientId, "sent", expectedDate: today.AddDays(-1)));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetOverdueAsync();

        // Assert: ascending by ExpectedDate → older first
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(olderId);
        result[1].Id.Should().Be(newerId);
    }

    // ─── GetReadyForDeliveryAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetReadyForDeliveryAsync_ReturnsOnlyReceivedStatus()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        var receivedId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(receivedId, patientId, "received"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "ready"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered"));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetReadyForDeliveryAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(receivedId);
    }

    // ─── GetPendingCountAsync ─────────────────────────────────────────────────

    [Theory]
    [InlineData("sent", true)]
    [InlineData("manufacturing", true)]
    [InlineData("tryIn", true)]
    [InlineData("remake", true)]
    [InlineData("draft", false)]
    [InlineData("ready", false)]
    [InlineData("received", false)]
    [InlineData("delivered", false)]
    [InlineData("cancelled", false)]
    public async Task GetPendingCountAsync_CountsOnlyInFlightStatuses(string status, bool counted)
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, status));
        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var count = await service.GetPendingCountAsync();

        // Assert
        count.Should().Be(counted ? 1 : 0);
    }

    // ─── GetTodayAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodayAsync_ReturnsOrdersWhereAnyKeyDateEqualsToday()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        var today = ClinicTimeProvider.ClinicToday();

        var sentTodayId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(sentTodayId, patientId, "sent", sentDate: today));

        var expectedTodayId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(expectedTodayId, patientId, "manufacturing", expectedDate: today));

        var receivedTodayId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(receivedTodayId, patientId, "received", receivedDate: today));

        var deliveredTodayId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(deliveredTodayId, patientId, "delivered", deliveredDate: today));

        // Not today — should be excluded
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent",
            sentDate: today.AddDays(-1), expectedDate: today.AddDays(1)));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetTodayAsync();

        // Assert
        var ids = result.Select(o => o.Id).ToHashSet();
        ids.Should().BeEquivalentTo(new[] { sentTodayId, expectedTodayId, receivedTodayId, deliveredTodayId });
    }

    // ─── GetReadyAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReadyAsync_ReturnsReadyAndReceivedOnly()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        var readyId = Guid.NewGuid();
        var receivedId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(readyId, patientId, "ready"));
        db.LabOrders.Add(CreateLabOrder(receivedId, patientId, "received"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered"));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetReadyAsync();

        // Assert
        var ids = result.Select(o => o.Id).ToHashSet();
        ids.Should().BeEquivalentTo(new[] { readyId, receivedId });
    }

    // ─── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetailDto_WithItemsOrderedBySortOrder()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var workTypeId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        db.Labs.Add(CreateLab(labId));
        db.LabWorkTypes.Add(new LabWorkType { Id = workTypeId, Name = "Crown", IsActive = true });

        var orderId = Guid.NewGuid();
        var order = CreateLabOrder(orderId, patientId, "sent", labId);
        order.Items.Add(new LabOrderItem
        {
            LabOrderId = orderId,
            WorkTypeId = workTypeId,
            SortOrder = 2,
            UnitsCount = 1,
            UnitPrice = 150,
            TotalPrice = 150
        });
        order.Items.Add(new LabOrderItem
        {
            LabOrderId = orderId,
            WorkTypeId = workTypeId,
            SortOrder = 1,
            UnitsCount = 2,
            UnitPrice = 100,
            TotalPrice = 200
        });
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetByIdAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(orderId);
        result.PatientName.Should().Be("أحمد محمد");
        result.LabEntityName.Should().Be("مختبر الأسنان");
        result.Items.Should().HaveCount(2);
        result.Items[0].SortOrder.Should().Be(1);
        result.Items[1].SortOrder.Should().Be(2);
        result.Items[0].TotalPrice.Should().Be(200m);
    }

    // ─── GetHistoryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_ReturnsNull_WhenOrderDoesNotExist()
    {
        // Preserves the original controller behavior: 404 if the order itself is missing.
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var result = await service.GetHistoryAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyList_WhenOrderExistsButNoHistory()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        var orderId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(orderId, patientId));
        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetHistoryAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEntries_OrderedByCreatedAtDescending()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        var orderId = Guid.NewGuid();
        db.LabOrders.Add(CreateLabOrder(orderId, patientId));

        // NOTE: AppDbContext.SaveChangesAsync override forcibly resets CreatedAt to
        // DateTime.UtcNow on Added entities. So we add+save first, then re-fetch and
        // set distinct CreatedAt values via the Modified path (which only touches
        // UpdatedAt, leaving CreatedAt intact).
        var olderEntryId = Guid.NewGuid();
        var newerEntryId = Guid.NewGuid();
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            Id = olderEntryId,
            LabOrderId = orderId,
            FromStatus = "draft",
            ToStatus = "sent"
        });
        db.LabOrderStatusHistories.Add(new LabOrderStatusHistory
        {
            Id = newerEntryId,
            LabOrderId = orderId,
            FromStatus = "sent",
            ToStatus = "manufacturing"
        });
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var older = await db.LabOrderStatusHistories.FindAsync(olderEntryId);
        var newer = await db.LabOrderStatusHistories.FindAsync(newerEntryId);
        older!.CreatedAt = now.AddDays(-2);
        newer!.CreatedAt = now;
        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var result = await service.GetHistoryAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result[0].ToStatus.Should().Be("manufacturing");
        result[1].ToStatus.Should().Be("sent");
    }

    // ─── GetAttachmentsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAttachmentsAsync_ReturnsEmptyList_WhenNoAttachments_EvenIfOrderDoesNotExist()
    {
        // Preserves the original controller behavior: no existence check on the order,
        // just returns whatever attachments exist (empty list if none).
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var result = await service.GetAttachmentsAsync(Guid.NewGuid());
        result.Should().BeEmpty();
    }

    // ─── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_AppliesStatusFilter_AndReturnsTotalCount()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "delivered"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "cancelled"));

        await db.SaveChangesAsync();

        // Act: filter by status="sent"
        var service = CreateService(db);
        var (orders, total) = await service.GetAllAsync(
            patientId: null, status: "sent", page: 1, pageSize: 20);

        // Assert
        orders.Should().HaveCount(2);
        total.Should().Be(2);
        orders.Should().OnlyContain(o => o.Status == "sent");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFinancialFields_WithoutDefaultingForeignCurrencyToYer()
    {
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));
        db.Labs.Add(CreateLab(labId));

        var order = CreateLabOrder(Guid.NewGuid(), patientId, "draft", labId, cost: 300m);
        order.TotalCost = 325m;
        order.Currency = "SAR";
        order.ExchangeRateToYer = 66m;
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (orders, total) = await service.GetAllAsync(
            patientId: patientId, status: null, page: 1, pageSize: 20);

        total.Should().Be(1);
        orders.Should().ContainSingle();
        orders[0].LabId.Should().Be(labId);
        orders[0].Cost.Should().Be(300m);
        orders[0].TotalCost.Should().Be(325m);
        orders[0].Currency.Should().Be("SAR");
        orders[0].ExchangeRateToYer.Should().Be(66m);
    }

    [Fact]
    public async Task GetAllAsync_AppliesPatientFilter()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientA = Guid.NewGuid();
        var patientB = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientA));
        db.Patients.Add(CreatePatient(patientB));

        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientA, "sent"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientA, "delivered"));
        db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientB, "sent"));

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var (orders, total) = await service.GetAllAsync(
            patientId: patientA, status: null, page: 1, pageSize: 20);

        // Assert
        orders.Should().HaveCount(2);
        total.Should().Be(2);
        orders.Should().OnlyContain(o => o.PatientId == patientA);
    }

    [Fact]
    public async Task GetAllAsync_PaginatesCorrectly()
    {
        // Arrange — 3 orders, page size 2, page 2 → returns 1 order + total=3
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        for (var i = 0; i < 3; i++)
        {
            db.LabOrders.Add(CreateLabOrder(Guid.NewGuid(), patientId, "sent",
                sentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i))));
        }

        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var (orders, total) = await service.GetAllAsync(
            patientId: null, status: null, page: 2, pageSize: 2);

        // Assert
        orders.Should().HaveCount(1);
        total.Should().Be(3);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        await using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(CreatePatient(patientId));

        var now = DateTime.UtcNow;
        var oldestId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var newestId = Guid.NewGuid();

        db.LabOrders.Add(CreateLabOrder(oldestId, patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(middleId, patientId, "sent"));
        db.LabOrders.Add(CreateLabOrder(newestId, patientId, "sent"));

        // Set CreatedAt explicitly via direct attach (CreateLabOrder doesn't expose it).
        // EF Core InMemory preserves the order of Add() calls for ordering by CreatedAt
        // when timestamps differ. We set timestamps after add by re-fetching.
        await db.SaveChangesAsync();
        var oldest = await db.LabOrders.FindAsync(oldestId);
        var middle = await db.LabOrders.FindAsync(middleId);
        var newest = await db.LabOrders.FindAsync(newestId);
        oldest!.CreatedAt = now.AddDays(-2);
        middle!.CreatedAt = now.AddDays(-1);
        newest!.CreatedAt = now;
        await db.SaveChangesAsync();

        // Act
        var service = CreateService(db);
        var (orders, total) = await service.GetAllAsync(
            patientId: null, status: null, page: 1, pageSize: 20);

        // Assert: newest first
        orders.Should().HaveCount(3);
        orders[0].Id.Should().Be(newestId);
        orders[1].Id.Should().Be(middleId);
        orders[2].Id.Should().Be(oldestId);
    }
}

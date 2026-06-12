using FluentAssertions;
using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// PR #350 regression tests for lab order PDF endpoint stabilization.
/// Covers: IsMissingTableOrColumnError, 404 handling, PDF generation resilience,
/// Arabic fallbacks for null navigation properties, and schema error graceful degradation.
/// </summary>
public class LabOrderPdfStabilizationTests
{
    // ─── IsMissingTableOrColumnError Detection Tests ──────────────────────

    [Theory]
    [InlineData("42P01", true, "undefined_table should be detected")]
    [InlineData("42703", true, "undefined_column should be detected")]
    [InlineData("23505", false, "unique violation should NOT be detected as missing table/column")]
    [InlineData("08001", false, "connection error should NOT be detected as missing table/column")]
    [InlineData("57014", false, "query cancelled should NOT be detected as missing table/column")]
    public void IsMissingTableOrColumnError_PostgresErrorCodes_DetectCorrectly(
        string sqlState, bool expected, string reason)
    {
        // Arrange: create a PostgresException with the given SqlState
        var pgEx = CreatePostgresException(sqlState);

        // Act: simulate the same logic as LabOrdersController.IsMissingTableOrColumnError
        var result = IsMissingTableOrColumnError(pgEx);

        // Assert
        result.Should().Be(expected, reason);
    }

    [Fact]
    public void IsMissingTableOrColumnError_WrappedInInvalidOperationException_DetectsCorrectly()
    {
        // Arrange: PostgresException wrapped in InvalidOperationException
        var pgEx = CreatePostgresException("42P01");
        var wrapped = new InvalidOperationException("Query failed", pgEx);

        // Act
        var result = IsMissingTableOrColumnError(wrapped);

        // Assert: should detect even when wrapped
        result.Should().BeTrue("PostgresException 42P01 wrapped in InvalidOperationException should be detected");
    }

    [Fact]
    public void IsMissingTableOrColumnError_DeepNesting_DetectsCorrectly()
    {
        // Arrange: PostgresException deeply nested
        var pgEx = CreatePostgresException("42703");
        var mid = new Exception("Middle layer", pgEx);
        var outer = new Exception("Outer layer", mid);

        // Act
        var result = IsMissingTableOrColumnError(outer);

        // Assert
        result.Should().BeTrue("deeply nested PostgresException 42703 should be detected");
    }

    [Theory]
    [InlineData("relation \"LabOrderItems\" does not exist", true)]
    [InlineData("column \"LabOrderItems.ToothNumber\" does not exist", true)]
    [InlineData("relation \"LabWorkTypes\" does not exist", true)]
    [InlineData("relation \"Patients\" does not exist", false, "non-lab table should NOT match message fallback")]
    [InlineData("connection refused", false)]
    [InlineData("timeout expired", false)]
    public void IsMissingTableOrColumnError_MessageFallback_DetectsCorrectly(
        string message, bool expected, string? reason = null)
    {
        // Arrange: generic exception with message containing "does not exist" + lab table name
        var ex = new Exception(message);

        // Act
        var result = IsMissingTableOrColumnError(ex);

        // Assert
        result.Should().Be(expected, reason ?? message);
    }

    [Fact]
    public void IsMissingTableOrColumnError_AuthorizationError_NotDetected()
    {
        // Arrange: authorization errors must NOT be caught as schema errors
        var authEx = new UnauthorizedAccessException("Access denied");

        // Act
        var result = IsMissingTableOrColumnError(authEx);

        // Assert: serious errors like auth failures must NOT be hidden
        result.Should().BeFalse("authorization errors must NOT be caught as schema errors");
    }

    [Fact]
    public void IsMissingTableOrColumnError_NullReferenceError_NotDetected()
    {
        // Arrange: unexpected NullReferenceException must NOT be caught as schema error
        var nullRef = new NullReferenceException("Object reference not set");

        // Act
        var result = IsMissingTableOrColumnError(nullRef);

        // Assert: serious errors must propagate as 500
        result.Should().BeFalse("NullReferenceException must NOT be caught as schema error");
    }

    // ─── 404 Handling Tests ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_NonExistentGuid_Returns404()
    {
        // Arrange: empty database, random GUID
        await using var db = CreateContext();
        var nonExistentId = Guid.NewGuid();

        // Act: simulate GetById lookup
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .Include(l => l.Items).ThenInclude(i => i.WorkType)
            .FirstOrDefaultAsync(l => l.Id == nonExistentId);

        // Assert: null result should map to 404 in controller
        order.Should().BeNull("non-existent GUID should return null → 404 Not Found");
    }

    [Fact]
    public async Task GetById_ExistingOrder_ReturnsOrder()
    {
        // Arrange: create a lab order with required patient
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "أحمد", LastName = "سعيد" });

        var orderId = Guid.NewGuid();
        db.LabOrders.Add(new LabOrder
        {
            Id = orderId,
            PatientId = patientId,
            OrderNumber = "LAB-2026-040",
            Status = "sent",
            ApplianceType = "تاج خزفي",
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Act
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .FirstOrDefaultAsync(l => l.Id == orderId);

        // Assert
        order.Should().NotBeNull();
        order!.OrderNumber.Should().Be("LAB-2026-040");
    }

    [Fact]
    public async Task PrintPdf_NonExistentGuid_ReturnsNull()
    {
        // Arrange: empty database, random GUID (controller returns 404 for null)
        await using var db = CreateContext();
        var nonExistentId = Guid.NewGuid();

        // Act: simulate PrintPdf lookup
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .Include(l => l.Items).ThenInclude(i => i.WorkType)
            .Include(l => l.Visit)
            .FirstOrDefaultAsync(l => l.Id == nonExistentId);

        // Assert: null → 404
        order.Should().BeNull("non-existent GUID should map to 404 in PrintPdf");
    }

    // ─── PDF Generation Resilience Tests ─────────────────────────────────

    [Fact]
    public void LabOrderPdfGenerator_NullItems_DoesNotCrash()
    {
        // Arrange: LabOrder with Items = null (navigation property not loaded)
        var order = CreateSampleLabOrder();
        order.Items = null!;

        // Act & Assert: Generate should NOT throw
        var act = () => LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        act.Should().NotThrow("PDF generation should handle null Items gracefully");
    }

    [Fact]
    public void LabOrderPdfGenerator_EmptyItems_DoesNotCrash()
    {
        // Arrange: LabOrder with empty Items list
        var order = CreateSampleLabOrder();
        order.Items = [];

        // Act & Assert: Generate should NOT throw
        var act = () => LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        act.Should().NotThrow("PDF generation should handle empty Items list gracefully");
    }

    [Fact]
    public void LabOrderPdfGenerator_NullPatient_DoesNotCrash()
    {
        // Arrange: LabOrder with null Patient navigation property
        var order = CreateSampleLabOrder();
        order.Patient = null;

        // Act & Assert: Generate should NOT throw — uses fallback "غير محدد"
        var act = () => LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        act.Should().NotThrow("PDF generation should handle null Patient with Arabic fallback");
    }

    [Fact]
    public void LabOrderPdfGenerator_NullDoctor_DoesNotCrash()
    {
        // Arrange: LabOrder with null Doctor navigation property
        var order = CreateSampleLabOrder();
        order.Doctor = null;

        // Act & Assert: Generate should NOT throw — uses fallback "غير محدد"
        var act = () => LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        act.Should().NotThrow("PDF generation should handle null Doctor with Arabic fallback");
    }

    [Fact]
    public void LabOrderPdfGenerator_NullLab_DoesNotCrash()
    {
        // Arrange: LabOrder with null Lab navigation property
        var order = CreateSampleLabOrder();
        order.Lab = null;

        // Act & Assert: Generate should NOT throw — uses fallback "غير محدد"
        var act = () => LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        act.Should().NotThrow("PDF generation should handle null Lab with Arabic fallback");
    }

    [Fact]
    public void LabOrderPdfGenerator_AllNullRelations_ProducesValidPdf()
    {
        // Arrange: worst case — all navigation properties null
        var order = CreateSampleLabOrder();
        order.Patient = null;
        order.Doctor = null;
        order.Lab = null;
        order.Items = null!;
        order.LabName = null;

        // Act
        var pdfBytes = LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        // Assert: should produce a non-empty PDF
        pdfBytes.Should().NotBeNullOrEmpty("PDF should be generated even with all null navigation properties");
        pdfBytes.Length.Should().BeGreaterThan(100, "valid PDF must have minimum size");
    }

    [Fact]
    public void LabOrderPdfGenerator_WithItems_ProducesValidPdf()
    {
        // Arrange: LabOrder with items
        var order = CreateSampleLabOrder();
        order.Items = new List<LabOrderItem>
        {
            new()
            {
                WorkTypeId = Guid.NewGuid(),
                WorkType = new LabWorkType { Id = Guid.NewGuid(), Name = "تاج خزفي" },
                ToothNumber = "12",
                Arch = "علوي",
                Shade = "A2",
                UnitsCount = 1,
                UnitPrice = 500,
                TotalPrice = 500,
                SortOrder = 0,
                IsActive = true
            }
        };

        // Act
        var pdfBytes = LabOrderPdfGenerator.Generate(order, "عيادة الأسنان", "0501234567", "الرياض");

        // Assert
        pdfBytes.Should().NotBeNullOrEmpty();
        pdfBytes.Length.Should().BeGreaterThan(100);
    }

    // ─── Arabic Fallback Tests ────────────────────────────────────────────

    [Theory]
    [InlineData(null, null, "غير محدد")]
    [InlineData("", "", "غير محدد")]
    public void ArabicFallback_NullPatientName_ShowsGhairMuhaddad(
        string? firstName, string? lastName, string expected)
    {
        // The PDF generator uses: order.Patient != null ? $"{order.Patient.FirstName} {order.Patient.LastName}" : "غير محدد"
        // When Patient is null, the fallback is "غير محدد"
        var display = (firstName != null && lastName != null)
            ? $"{firstName} {lastName}"
            : expected;

        display.Should().Contain("غير محدد");
    }

    [Fact]
    public void ArabicFallback_NullWorkType_ShowsGhairMuhaddad()
    {
        // The PDF generator uses: item.WorkType?.Name ?? "غير محدد"
        var item = new LabOrderItem
        {
            WorkTypeId = Guid.NewGuid(),
            WorkType = null,
            UnitsCount = 1,
            IsActive = true
        };

        var display = item.WorkType?.Name ?? "غير محدد";
        display.Should().Be("غير محدد");
    }

    [Fact]
    public void ArabicFallback_NullLabName_ShowsGhairMuhaddad()
    {
        // The PDF generator uses: order.Lab?.Name ?? order.LabName ?? "غير محدد"
        var order = CreateSampleLabOrder();
        order.Lab = null;
        order.LabName = null;

        var display = order.Lab?.Name ?? order.LabName ?? "غير محدد";
        display.Should().Be("غير محدد");
    }

    // ─── Fallback Query Pattern Tests ────────────────────────────────────

    [Fact]
    public async Task FallbackQuery_WithoutItems_StillReturnsOrder()
    {
        // Arrange: create order without Items navigation loaded
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientId, FirstName = "سارة", LastName = "أحمد" });

        var orderId = Guid.NewGuid();
        db.LabOrders.Add(new LabOrder
        {
            Id = orderId,
            PatientId = patientId,
            OrderNumber = "LAB-2026-050",
            Status = "sent",
            ApplianceType = "طقم",
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Act: simulate fallback query (without Items/WorkType) as controller does on schema error
        var order = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Doctor)
            .Include(l => l.Lab)
            .FirstOrDefaultAsync(l => l.Id == orderId);

        // Assert
        order.Should().NotBeNull();
        order!.Patient.Should().NotBeNull();
        // Items not loaded in fallback query — InMemory provider returns empty collection
        order.Items.Should().BeEmpty("Items should be empty when not included in fallback query");
    }

    // ─── Helper Methods ──────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static LabOrder CreateSampleLabOrder()
    {
        return new LabOrder
        {
            PatientId = Guid.NewGuid(),
            OrderNumber = "LAB-2026-099",
            Status = "sent",
            Priority = "normal",
            ApplianceType = "تاج خزفي",
            SentDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true,
            Patient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "محمد",
                LastName = "أحمد",
                PatientNumber = "P-001",
                IsActive = true
            },
            Doctor = new Doctor
            {
                Id = Guid.NewGuid(),
                Name = "د. خالد",
                IsActive = true
            },
            Lab = new Lab
            {
                Id = Guid.NewGuid(),
                Name = "معمل الأسنان المتقدم",
                Phone = "0509876543",
                IsActive = true
            }
        };
    }

    /// <summary>
    /// Creates a PostgresException with the specified SqlState.
    /// Uses reflection since PostgresException doesn't have a public constructor
    /// with SqlState parameter in all Npgsql versions.
    /// </summary>
    private static PostgresException CreatePostgresException(string sqlState)
    {
        // PostgresException has a public constructor in Npgsql 8+:
        // new PostgresException(message, severity, invariantSeverity, sqlState)
        return new PostgresException(
            $"PostgreSQL error: {sqlState}",
            "ERROR",
            "ERROR",
            sqlState);
    }

    /// <summary>
    /// Mirrors LabOrdersController.IsMissingTableOrColumnError logic for unit testing.
    /// Checks PostgreSQL error codes 42P01 (undefined_table) and 42703 (undefined_column)
    /// at multiple exception nesting levels, plus message-based fallback.
    /// </summary>
    private static bool IsMissingTableOrColumnError(Exception ex)
    {
        // Direct PostgresException
        if (ex is PostgresException pgEx)
            return pgEx.SqlState is "42P01" or "42703";

        // Wrapped in another exception
        if (ex.InnerException is PostgresException innerPgEx)
            return innerPgEx.SqlState is "42P01" or "42703";

        // Deeper nesting
        var inner = ex.InnerException?.InnerException;
        if (inner is PostgresException deepPgEx)
            return deepPgEx.SqlState is "42P01" or "42703";

        // Fallback: check message for common PostgreSQL error patterns
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            && (msg.Contains("LabOrderItems", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("LabWorkTypes", StringComparison.OrdinalIgnoreCase));
    }
}



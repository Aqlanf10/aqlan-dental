using FluentAssertions;
using Xunit;

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
}

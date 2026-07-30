using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Commissions;

/// <summary>
/// CORE-FIN-LAB-ADJ: the deduction rule itself, tested away from any database.
/// Every case here is one the clinic actually hits — a draft that must not be billed to the
/// doctor, and a lab that invoices in Saudi riyals while the patient is billed in Yemeni ones.
/// </summary>
public class LabCostForCommissionTests
{
    private static LabCostForCommission.Input Order(
        string status = "sent",
        bool isActive = true,
        decimal? totalCost = 20_000m,
        decimal? cost = 20_000m,
        string labCurrency = "YER",
        decimal rate = 1m,
        string invoiceCurrency = "YER")
        => new(status, isActive, totalCost, cost, labCurrency, rate, invoiceCurrency);

    [Theory]
    [InlineData("sent")]
    [InlineData("received")]
    [InlineData("ready")]
    [InlineData("delivered")]
    [InlineData("remake")]
    public void CommittedStatuses_AreDeducted(string status)
    {
        var r = LabCostForCommission.Resolve(Order(status: status));

        r.ShouldWrite.Should().BeTrue();
        r.Amount.Should().Be(20_000m);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("DRAFT")]
    [InlineData(" Draft ")]
    public void ADraft_LeavesTheLineAlone_RatherThanWritingEitherNumber(string status)
    {
        // Writing the draft's cost would deduct a bill the clinic has not committed to;
        // writing zero would discard the service's own estimate. Neither is an answer.
        var r = LabCostForCommission.Resolve(Order(status: status));

        r.ShouldWrite.Should().BeFalse();
        r.NeedsAttention.Should().BeFalse("a draft is the ordinary case, not a problem");
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData(" Cancelled ")]
    public void ACancelledOrder_WritesZero_SoTheDeductionIsReleased(string status)
    {
        var r = LabCostForCommission.Resolve(Order(status: status));

        r.ShouldWrite.Should().BeTrue("cancellation IS a decision — the cost is definitively nil");
        r.Amount.Should().Be(0m);
    }

    [Fact]
    public void SoftDeletedOrder_WritesZero()
    {
        var r = LabCostForCommission.Resolve(Order(isActive: false));

        r.ShouldWrite.Should().BeTrue();
        r.Amount.Should().Be(0m);
    }

    [Fact]
    public void TotalCost_WinsOverTheCostSnapshot()
    {
        // CLIN-08: remakes grow TotalCost while Cost keeps the original figure.
        var r = LabCostForCommission.Resolve(Order(totalCost: 26_000m, cost: 20_000m));

        r.Amount.Should().Be(26_000m);
    }

    [Fact]
    public void ForeignLabCurrency_IsConvertedToYer()
    {
        var r = LabCostForCommission.Resolve(
            Order(totalCost: 100m, cost: 100m, labCurrency: "SAR", rate: 68m, invoiceCurrency: "YER"));

        r.ShouldWrite.Should().BeTrue();
        r.Amount.Should().Be(6_800m);
    }

    [Fact]
    public void ForeignLabCurrencyWithNoRate_IsRefusedRatherThanTreatedAsOneToOne()
    {
        var r = LabCostForCommission.Resolve(
            Order(totalCost: 100m, cost: 100m, labCurrency: "SAR", rate: 0m, invoiceCurrency: "YER"));

        r.ShouldWrite.Should().BeFalse();
        r.NeedsAttention.Should().BeTrue();
        r.Reason.Should().Contain("سعر صرف");
    }

    [Fact]
    public void InvoiceAndLabInDifferentForeignCurrencies_AreRefused()
    {
        var r = LabCostForCommission.Resolve(
            Order(totalCost: 100m, cost: 100m, labCurrency: "SAR", rate: 68m, invoiceCurrency: "USD"));

        r.ShouldWrite.Should().BeFalse();
        r.NeedsAttention.Should().BeTrue();
        r.Reason.Should().Contain("USD").And.Contain("SAR");
    }

    [Fact]
    public void SameForeignCurrencyOnBothSides_NeedsNoConversion()
    {
        var r = LabCostForCommission.Resolve(
            Order(totalCost: 250m, cost: 250m, labCurrency: "USD", rate: 530m, invoiceCurrency: "USD"));

        r.ShouldWrite.Should().BeTrue();
        r.Amount.Should().Be(250m);
    }

    [Fact]
    public void MissingCurrency_IsTreatedAsYer_MatchingTheEntityDefault()
    {
        var r = LabCostForCommission.Resolve(
            Order(labCurrency: "", invoiceCurrency: null!));

        r.ShouldWrite.Should().BeTrue();
        r.Amount.Should().Be(20_000m);
    }

    [Fact]
    public void ZeroOrMissingCost_DeductsNothing()
    {
        LabCostForCommission.Resolve(Order(totalCost: null, cost: null)).Amount.Should().Be(0m);
        LabCostForCommission.Resolve(Order(totalCost: 0m, cost: 0m)).Amount.Should().Be(0m);
    }
}

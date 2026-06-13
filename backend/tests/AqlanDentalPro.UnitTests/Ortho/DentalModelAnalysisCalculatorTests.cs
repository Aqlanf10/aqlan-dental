using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class DentalModelAnalysisCalculatorTests
{
    [Fact]
    public void Bolton_CompletePermanentDentition_ComputesRatiosAndDiscrepancy()
    {
        var input = CompleteInput(upperWidth: 8m, lowerWidth: 7.3m);

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Bolton.Should().NotBeNull();
        result.Bolton!.OverallRatio.Should().Be(91.25m);
        result.Bolton.AnteriorRatio.Should().Be(91.25m);
        result.Bolton.OverallDiscrepancy.Should().Be(-0.05m);
    }

    [Fact]
    public void ArchPerimeter_AvailableMinusRequired_ReportsCrowdingAndSpacing()
    {
        var input = CompleteInput() with
        {
            UpperAvailableSpace = 92m,
            LowerAvailableSpace = 90m,
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.UpperArch!.Discrepancy.Should().Be(-4m);
        result.UpperArch.Interpretation.Should().Contain("ازدحام");
        result.LowerArch!.Discrepancy.Should().Be(2.4m);
        result.LowerArch.Interpretation.Should().Contain("فراغ");
    }

    [Fact]
    public void Pont_UpperIncisorSum_ComputesPremolarAndMolarPredictions()
    {
        var input = CompleteInput() with
        {
            UpperInterpremolarWidth = 38m,
            UpperIntermolarWidth = 48m,
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Pont!.IncisorSum.Should().Be(32m);
        result.Pont.PredictedInterpremolarWidth.Should().Be(40m);
        result.Pont.PredictedIntermolarWidth.Should().Be(50m);
        result.Pont.PremolarDifference.Should().Be(-2m);
        result.Pont.MolarDifference.Should().Be(-2m);
    }

    [Fact]
    public void AshleyHowe_ComputesPmdAndPmbawPercentages()
    {
        var input = CompleteInput() with
        {
            HowePremolarDiameter = 44m,
            HowePremolarBasalArchWidth = 42m,
            HoweBasalArchLength = 35m,
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Howe!.TotalToothMaterial.Should().Be(96m);
        result.Howe.PremolarDiameterPercent.Should().Be(45.83m);
        result.Howe.PremolarBasalArchWidthPercent.Should().Be(43.75m);
        result.Howe.Interpretation.Should().Contain("حدّية");
    }

    [Fact]
    public void Moyers_ExactTableValueAt75thPercentile_IsReturned()
    {
        var input = CompleteInput() with
        {
            MoyersPercentile = 75,
            ToothWidths = CompleteInput().ToothWidths
                .Concat(new[]
                {
                    new KeyValuePair<string, decimal?>("32", 5.875m),
                    new KeyValuePair<string, decimal?>("31", 5.875m),
                    new KeyValuePair<string, decimal?>("41", 5.875m),
                    new KeyValuePair<string, decimal?>("42", 5.875m),
                })
                .GroupBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Last().Value),
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Moyers!.LowerIncisorSum.Should().Be(23.5m);
        result.Moyers.Prediction.PredictedUpperPerSide.Should().Be(22.9m);
        result.Moyers.Prediction.PredictedLowerPerSide.Should().Be(22.5m);
    }

    [Fact]
    public void Moyers_BetweenColumns_InterpolatesLinearly()
    {
        var input = CompleteInput() with
        {
            MoyersPercentile = 75,
            ToothWidths = LowerIncisorWidths(23.25m),
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Moyers!.Prediction.PredictedUpperPerSide.Should().Be(22.75m);
        result.Moyers.Prediction.PredictedLowerPerSide.Should().Be(22.35m);
    }

    [Fact]
    public void TanakaJohnston_UsesPublishedHalfIncisorSumEquations()
    {
        var input = CompleteInput() with { ToothWidths = LowerIncisorWidths(22m) };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.TanakaJohnston!.Prediction.PredictedUpperPerSide.Should().Be(22m);
        result.TanakaJohnston.Prediction.PredictedLowerPerSide.Should().Be(21.5m);
    }

    [Fact]
    public void Huckaba_CorrectsRadiographicMagnification()
    {
        var input = CompleteInput() with
        {
            HuckabaTeeth =
            [
                new HuckabaToothInput("13", 8m, 7.5m, 7.2m),
            ],
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Huckaba.Should().ContainSingle();
        result.Huckaba[0].PredictedActualWidth.Should().Be(8.33m);
    }

    [Fact]
    public void IncompleteToothSets_DoNotProduceMisleadingBoltonResult()
    {
        var input = CompleteInput() with
        {
            ToothWidths = new Dictionary<string, decimal?>
            {
                ["11"] = 8m,
                ["21"] = 8m,
            },
        };

        var result = DentalModelAnalysisCalculator.Calculate(input);

        result.Bolton.Should().BeNull();
        result.UpperArch.Should().BeNull();
        result.Moyers.Should().BeNull();
    }

    private static DentalModelAnalysisInput CompleteInput(
        decimal upperWidth = 8m,
        decimal lowerWidth = 7.3m)
    {
        var widths = new Dictionary<string, decimal?>();
        foreach (var code in new[] { "16", "15", "14", "13", "12", "11", "21", "22", "23", "24", "25", "26" })
            widths[code] = upperWidth;
        foreach (var code in new[] { "36", "35", "34", "33", "32", "31", "41", "42", "43", "44", "45", "46" })
            widths[code] = lowerWidth;

        return new DentalModelAnalysisInput(
            widths,
            UpperAvailableSpace: null,
            LowerAvailableSpace: null,
            UpperInterpremolarWidth: null,
            UpperIntermolarWidth: null,
            HowePremolarDiameter: null,
            HowePremolarBasalArchWidth: null,
            HoweBasalArchLength: null,
            MixedUpperAvailablePerSide: null,
            MixedLowerAvailablePerSide: null,
            MoyersPercentile: 75,
            HuckabaTeeth: []);
    }

    private static Dictionary<string, decimal?> LowerIncisorWidths(decimal total)
    {
        var input = CompleteInput();
        var perTooth = total / 4m;
        foreach (var code in new[] { "32", "31", "41", "42" })
            input.ToothWidths[code] = perTooth;
        return input.ToothWidths;
    }
}

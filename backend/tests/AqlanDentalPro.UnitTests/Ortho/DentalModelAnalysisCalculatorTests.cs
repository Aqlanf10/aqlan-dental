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

    // ═══════════════════════════════════════════════════════════════════════════
    // QA-599: Tests for the 7 new analyses ported from the Android app
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void QA599_ArchPerimeter_SevereCrowding_ReturnsCorrectDiagnosis()
    {
        var result = DentalModelAnalysisCalculator.CalculateArchPerimeter(70m, 80m);
        result.Should().NotBeNull();
        result!.Discrepancy.Should().Be(-10m);
        result.Diagnosis.Should().Contain("تزاحم");
        result.Comment.Should().Contain("تزاحم شديد");
    }

    [Fact]
    public void QA599_ArchPerimeter_MildCrowding_ReturnsCorrectComment()
    {
        var result = DentalModelAnalysisCalculator.CalculateArchPerimeter(78m, 80m);
        result!.Discrepancy.Should().Be(-2m);
        result.Comment.Should().Contain("تزاحم خفيف");
        result.Comment.Should().Contain("IPR");
    }

    [Fact]
    public void QA599_ArchPerimeter_Spacing_ReturnsCorrectDiagnosis()
    {
        var result = DentalModelAnalysisCalculator.CalculateArchPerimeter(85m, 80m);
        result!.Discrepancy.Should().Be(5m);
        result.Diagnosis.Should().Contain("فراغات");
    }

    [Fact]
    public void QA599_ArchPerimeter_NullInput_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculateArchPerimeter(null, 80m).Should().BeNull();
        DentalModelAnalysisCalculator.CalculateArchPerimeter(80m, null).Should().BeNull();
        DentalModelAnalysisCalculator.CalculateArchPerimeter(0m, 80m).Should().BeNull();
    }

    [Fact]
    public void QA599_AshleyHowe_Below44Percent_ReturnsDeficiency()
    {
        var result = DentalModelAnalysisCalculator.CalculateAshleyHowe(40m, 16m, 17m);
        result!.BasalArchPercent.Should().Be(42.5m);
        result.Interpretation.Should().Contain("نقص");
        result.ExpansionPossibility.Should().Contain("ممكن"); // 17 > 16
    }

    [Fact]
    public void QA599_AshleyHowe_Above44Percent_ReturnsAcceptable()
    {
        var result = DentalModelAnalysisCalculator.CalculateAshleyHowe(40m, 18m, 19m);
        result!.BasalArchPercent.Should().Be(47.5m);
        result.Interpretation.Should().Contain("مناسب");
    }

    [Fact]
    public void QA599_AshleyHowe_PmbawLessThanPmd_ExpansionLimited()
    {
        var result = DentalModelAnalysisCalculator.CalculateAshleyHowe(40m, 20m, 18m);
        result!.ExpansionPossibility.Should().Contain("محدود");
    }

    [Fact]
    public void QA599_LinderHarth_ComputesCorrectCoefficients()
    {
        // SI=34 → CPV = 34×100/85 = 40, CMV = 34×100/65 = 52.31
        var result = DentalModelAnalysisCalculator.CalculateLinderHarth(34m, 38m, 50m);
        result!.PredictedInterpremolarWidth.Should().Be(40m);
        result.PredictedIntermolarWidth.Should().BeApproximately(52.31m, 0.01m);
        result.PremolarDifference.Should().Be(2m); // 40 - 38 = 2 → tight
        result.PremolarDiagnosis.Should().Contain("توسعة");
    }

    [Fact]
    public void QA599_LinderHarth_NullSi_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculateLinderHarth(null, 38m, 50m).Should().BeNull();
    }

    [Fact]
    public void QA599_PeckPeck_AllFourIncisors_ComputesIndices()
    {
        // Central: MD=8, FL=8.5 → index = 94.1% (> 91 → wide)
        // Lateral: MD=7, FL=7.5 → index = 93.3% (< 94 → ok)
        var result = DentalModelAnalysisCalculator.CalculatePeckPeck(
            md31: 8m, fl31: 8.5m,
            md32: 7m, fl32: 7.5m,
            md41: 8m, fl41: 8.5m,
            md42: 7m, fl42: 7.5m);
        result!.Teeth.Should().HaveCount(4);
        result.Teeth[0].Index.Should().BeApproximately(94.12m, 0.01m);
        result.Teeth[0].Diagnosis.Should().Contain("عريض");
        result.Teeth[1].Index.Should().BeApproximately(93.33m, 0.01m);
        result.Teeth[1].Diagnosis.Should().Contain("متناسقة");
    }

    [Fact]
    public void QA599_PeckPeck_AllNull_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculatePeckPeck(null, null, null, null, null, null, null, null)
            .Should().BeNull();
    }

    [Fact]
    public void QA599_Korkhaus_ComputesPredictedLength()
    {
        // SI=32 → predicted = 32×100/160 = 20
        var result = DentalModelAnalysisCalculator.CalculateKorkhaus(32m, 22m);
        result!.PredictedArchLength.Should().Be(20m);
        result.Difference.Should().Be(2m);
        result.Diagnosis.Should().Contain("أكبر");
    }

    [Fact]
    public void QA599_Korkhaus_NullSi_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculateKorkhaus(null, 22m).Should().BeNull();
    }

    [Fact]
    public void QA599_NanceMixed_BothArches_ComputesDiscrepancies()
    {
        var result = DentalModelAnalysisCalculator.CalculateNanceMixed(
            maxAvailable: 70m, maxRequired: 75m,
            mandAvailable: 65m, mandRequired: 60m);
        result!.MaxDiscrepancy.Should().Be(-5m);
        result.MaxDiagnosis.Should().Contain("نقص");
        result.MandDiscrepancy.Should().Be(5m);
        result.MandDiagnosis.Should().Contain("فراغ");
    }

    [Fact]
    public void QA599_NanceMixed_OnlyMax_ReturnsWithoutMand()
    {
        var result = DentalModelAnalysisCalculator.CalculateNanceMixed(
            maxAvailable: 70m, maxRequired: 70m,
            mandAvailable: null, mandRequired: null);
        result!.MaxDiscrepancy.Should().Be(0m);
        result.MandDiscrepancy.Should().BeNull();
    }

    [Fact]
    public void QA599_NanceMixed_AllNull_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculateNanceMixed(null, null, null, null)
            .Should().BeNull();
    }

    [Fact]
    public void QA599_HuckabaY1_ComputesCompensatedWidth()
    {
        // X1=8, X2=10, Y2=7.5 → Y1 = 8×7.5/10 = 6
        var result = DentalModelAnalysisCalculator.CalculateHuckabaY1(8m, 10m, 7.5m);
        result.Should().Be(6m);
    }

    [Fact]
    public void QA599_HuckabaY1_ZeroX2_ReturnsNull()
    {
        DentalModelAnalysisCalculator.CalculateHuckabaY1(8m, 0m, 7.5m).Should().BeNull();
    }

    [Fact]
    public void QA599_CalculateExtended_AllFieldsNull_ReturnsNullAnalyses()
    {
        var input = CompleteInput();
        var baseResult = DentalModelAnalysisCalculator.Calculate(input);
        var extended = DentalModelAnalysisCalculator.CalculateExtended(input, baseResult);
        extended.ArchPerimeter.Should().BeNull();
        extended.Careys.Should().BeNull();
        extended.AshleyHowe.Should().BeNull();
        extended.LinderHarth.Should().BeNull();
        extended.PeckPeck.Should().BeNull();
        extended.Korkhaus.Should().BeNull();
        extended.NanceMixed.Should().BeNull();
        extended.Base.Should().Be(baseResult);
    }

    [Fact]
    public void QA599_CalculateExtended_WithAllFields_ReturnsAllResults()
    {
        var input = CompleteInput();
        var baseResult = DentalModelAnalysisCalculator.Calculate(input);
        var extended = DentalModelAnalysisCalculator.CalculateExtended(
            input, baseResult,
            ashleyHoweTtm: 40m, ashleyHowePmd: 16m, ashleyHowePmbaw: 17m,
            linderHarthSi: 34m, linderHarthMeasuredPmv: 38m, linderHarthMeasuredMv: 50m,
            peckMd31: 8m, peckFl31: 8.5m, peckMd32: 7m, peckFl32: 7.5m,
            peckMd41: 8m, peckFl41: 8.5m, peckMd42: 7m, peckFl42: 7.5m,
            korkhausSi: 32m, korkhausMeasuredLength: 22m,
            nanceMaxAvailable: 70m, nanceMaxRequired: 75m,
            nanceMandAvailable: 65m, nanceMandRequired: 60m,
            archPerimeterAvailable: 70m, archPerimeterRequired: 80m,
            careysAvailable: 78m, careysRequired: 80m);
        extended.ArchPerimeter.Should().NotBeNull();
        extended.Careys.Should().NotBeNull();
        extended.AshleyHowe.Should().NotBeNull();
        extended.LinderHarth.Should().NotBeNull();
        extended.PeckPeck.Should().NotBeNull();
        extended.Korkhaus.Should().NotBeNull();
        extended.NanceMixed.Should().NotBeNull();
    }
}

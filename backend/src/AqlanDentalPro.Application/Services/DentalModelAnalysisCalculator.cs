namespace AqlanDentalPro.Application.Services;

public sealed record HuckabaToothInput(
    string ToothCode,
    decimal RadiographicUneruptedWidth,
    decimal ActualReferenceWidth,
    decimal RadiographicReferenceWidth);

public sealed record DentalModelAnalysisInput(
    Dictionary<string, decimal?> ToothWidths,
    decimal? UpperAvailableSpace,
    decimal? LowerAvailableSpace,
    decimal? UpperInterpremolarWidth,
    decimal? UpperIntermolarWidth,
    decimal? HowePremolarDiameter,
    decimal? HowePremolarBasalArchWidth,
    decimal? HoweBasalArchLength,
    decimal? MixedUpperAvailablePerSide,
    decimal? MixedLowerAvailablePerSide,
    int MoyersPercentile,
    List<HuckabaToothInput> HuckabaTeeth);

public sealed record BoltonResult(
    decimal OverallRatio,
    decimal AnteriorRatio,
    decimal OverallDiscrepancy,
    decimal AnteriorDiscrepancy,
    string OverallInterpretation,
    string AnteriorInterpretation);

public sealed record ArchSpaceResult(
    decimal Required,
    decimal Available,
    decimal Discrepancy,
    string Interpretation);

public sealed record PontResult(
    decimal IncisorSum,
    decimal PredictedInterpremolarWidth,
    decimal PredictedIntermolarWidth,
    decimal? MeasuredInterpremolarWidth,
    decimal? MeasuredIntermolarWidth,
    decimal? PremolarDifference,
    decimal? MolarDifference);

public sealed record HoweResult(
    decimal TotalToothMaterial,
    decimal PremolarDiameterPercent,
    decimal PremolarBasalArchWidthPercent,
    decimal? BasalArchLength,
    string Interpretation);

public sealed record MixedDentitionPrediction(
    decimal PredictedUpperPerSide,
    decimal PredictedLowerPerSide,
    decimal? UpperSpaceDiscrepancyPerSide,
    decimal? LowerSpaceDiscrepancyPerSide);

public sealed record MoyersResult(
    int Percentile,
    decimal LowerIncisorSum,
    MixedDentitionPrediction Prediction);

public sealed record TanakaJohnstonResult(
    decimal LowerIncisorSum,
    MixedDentitionPrediction Prediction);

public sealed record HuckabaToothResult(
    string ToothCode,
    decimal PredictedActualWidth);

public sealed record DentalModelAnalysisResult(
    BoltonResult? Bolton,
    ArchSpaceResult? UpperArch,
    ArchSpaceResult? LowerArch,
    PontResult? Pont,
    HoweResult? Howe,
    MoyersResult? Moyers,
    TanakaJohnstonResult? TanakaJohnston,
    List<HuckabaToothResult> Huckaba,
    List<string> Warnings);

public static class DentalModelAnalysisCalculator
{
    private static readonly string[] UpperOverall =
        ["16", "15", "14", "13", "12", "11", "21", "22", "23", "24", "25", "26"];
    private static readonly string[] LowerOverall =
        ["36", "35", "34", "33", "32", "31", "41", "42", "43", "44", "45", "46"];
    private static readonly string[] UpperAnterior = ["13", "12", "11", "21", "22", "23"];
    private static readonly string[] LowerAnterior = ["33", "32", "31", "41", "42", "43"];
    private static readonly string[] UpperIncisors = ["12", "11", "21", "22"];
    private static readonly string[] LowerIncisors = ["32", "31", "41", "42"];

    private static readonly decimal[] MoyersIncisorSums =
        [19.5m, 20.0m, 20.5m, 21.0m, 21.5m, 22.0m, 22.5m, 23.0m, 23.5m, 24.0m, 24.5m, 25.0m];

    private static readonly IReadOnlyDictionary<int, decimal[]> MoyersUpper =
        new Dictionary<int, decimal[]>
        {
            [95] = [21.6m, 21.8m, 22.1m, 22.4m, 22.7m, 22.9m, 23.2m, 23.5m, 23.8m, 24.0m, 24.3m, 24.6m],
            [85] = [21.0m, 21.3m, 21.5m, 21.8m, 22.1m, 22.4m, 22.6m, 22.9m, 23.2m, 23.5m, 23.7m, 24.0m],
            [75] = [20.6m, 20.9m, 21.2m, 21.5m, 21.8m, 22.0m, 22.3m, 22.6m, 22.9m, 23.1m, 23.4m, 23.7m],
            [65] = [20.4m, 20.6m, 20.9m, 21.2m, 21.5m, 21.8m, 22.0m, 22.3m, 22.6m, 22.8m, 23.1m, 23.4m],
            [50] = [20.0m, 20.3m, 20.6m, 20.8m, 21.1m, 21.4m, 21.7m, 21.9m, 22.2m, 22.5m, 22.8m, 23.0m],
            [35] = [19.6m, 19.9m, 20.2m, 20.5m, 20.8m, 21.0m, 21.3m, 21.6m, 21.9m, 22.1m, 22.4m, 22.7m],
            [25] = [19.4m, 19.7m, 19.9m, 20.2m, 20.5m, 20.8m, 21.0m, 21.3m, 21.6m, 21.9m, 22.1m, 22.4m],
            [15] = [19.0m, 19.3m, 19.6m, 19.9m, 20.2m, 20.4m, 20.7m, 21.0m, 21.3m, 21.5m, 21.8m, 22.1m],
            [5] = [18.5m, 18.8m, 19.0m, 19.3m, 19.6m, 19.9m, 20.1m, 20.4m, 20.7m, 21.0m, 21.2m, 21.5m],
        };

    private static readonly IReadOnlyDictionary<int, decimal[]> MoyersLower =
        new Dictionary<int, decimal[]>
        {
            [95] = [21.1m, 21.4m, 21.7m, 22.0m, 22.3m, 22.6m, 22.9m, 23.2m, 23.5m, 23.8m, 24.1m, 24.4m],
            [85] = [20.5m, 20.8m, 21.1m, 21.4m, 21.7m, 22.0m, 22.3m, 22.6m, 22.9m, 23.2m, 23.5m, 23.8m],
            [75] = [20.1m, 20.4m, 20.7m, 21.0m, 21.3m, 21.6m, 21.9m, 22.2m, 22.5m, 22.8m, 23.1m, 23.4m],
            [65] = [19.8m, 20.1m, 20.4m, 20.7m, 21.0m, 21.3m, 21.6m, 21.9m, 22.2m, 22.5m, 22.8m, 23.1m],
            [50] = [19.4m, 19.7m, 20.0m, 20.3m, 20.6m, 20.9m, 21.2m, 21.5m, 21.8m, 22.1m, 22.4m, 22.7m],
            [35] = [19.0m, 19.3m, 19.6m, 19.9m, 20.2m, 20.5m, 20.8m, 21.1m, 21.4m, 21.7m, 22.0m, 22.3m],
            [25] = [18.7m, 19.0m, 19.3m, 19.6m, 19.9m, 20.2m, 20.5m, 20.8m, 21.1m, 21.4m, 21.7m, 22.0m],
            [15] = [18.4m, 18.7m, 19.0m, 19.3m, 19.6m, 19.8m, 20.1m, 20.4m, 20.7m, 21.0m, 21.3m, 21.6m],
            [5] = [17.7m, 18.0m, 18.3m, 18.6m, 18.9m, 19.2m, 19.5m, 19.8m, 20.1m, 20.4m, 20.7m, 21.0m],
        };

    public static DentalModelAnalysisResult Calculate(DentalModelAnalysisInput input)
    {
        var warnings = new List<string>();
        var upperOverall = SumIfComplete(input.ToothWidths, UpperOverall);
        var lowerOverall = SumIfComplete(input.ToothWidths, LowerOverall);
        var upperAnterior = SumIfComplete(input.ToothWidths, UpperAnterior);
        var lowerAnterior = SumIfComplete(input.ToothWidths, LowerAnterior);
        var upperIncisors = SumIfComplete(input.ToothWidths, UpperIncisors);
        var lowerIncisors = SumIfComplete(input.ToothWidths, LowerIncisors);

        BoltonResult? bolton = null;
        if (upperOverall is > 0 && lowerOverall is > 0 && upperAnterior is > 0 && lowerAnterior is > 0)
        {
            var overallRatio = Round(lowerOverall.Value / upperOverall.Value * 100);
            var anteriorRatio = Round(lowerAnterior.Value / upperAnterior.Value * 100);
            var overallDiscrepancy = Round(BoltonDiscrepancy(upperOverall.Value, lowerOverall.Value, 0.913m));
            var anteriorDiscrepancy = Round(BoltonDiscrepancy(upperAnterior.Value, lowerAnterior.Value, 0.772m));
            bolton = new BoltonResult(
                overallRatio,
                anteriorRatio,
                overallDiscrepancy,
                anteriorDiscrepancy,
                InterpretBolton(overallDiscrepancy),
                InterpretBolton(anteriorDiscrepancy));
        }

        var upperArch = BuildArchResult(upperOverall, input.UpperAvailableSpace);
        var lowerArch = BuildArchResult(lowerOverall, input.LowerAvailableSpace);

        PontResult? pont = null;
        if (upperIncisors is > 0)
        {
            var premolar = Round(upperIncisors.Value * 100m / 80m);
            var molar = Round(upperIncisors.Value * 100m / 64m);
            pont = new PontResult(
                Round(upperIncisors.Value),
                premolar,
                molar,
                input.UpperInterpremolarWidth,
                input.UpperIntermolarWidth,
                input.UpperInterpremolarWidth.HasValue
                    ? Round(input.UpperInterpremolarWidth.Value - premolar)
                    : null,
                input.UpperIntermolarWidth.HasValue
                    ? Round(input.UpperIntermolarWidth.Value - molar)
                    : null);
            warnings.Add("مؤشر Pont مرجع وصفي فقط؛ موثوقيته تختلف باختلاف المجتمع وشكل القوس ولا يحدد قرار التوسيع وحده.");
        }

        HoweResult? howe = null;
        if (upperOverall is > 0 &&
            input.HowePremolarDiameter is > 0 &&
            input.HowePremolarBasalArchWidth is > 0)
        {
            var pmdPercent = Round(input.HowePremolarDiameter.Value / upperOverall.Value * 100m);
            var pmbawPercent = Round(input.HowePremolarBasalArchWidth.Value / upperOverall.Value * 100m);
            howe = new HoweResult(
                Round(upperOverall.Value),
                pmdPercent,
                pmbawPercent,
                input.HoweBasalArchLength,
                pmbawPercent <= 37m
                    ? "نقص واضح في عرض القاعدة القمية؛ يلزم تقييم خيارات الخلع أو البدائل ضمن التشخيص الكامل."
                    : pmbawPercent >= 44m
                        ? "عرض القاعدة القمية ملائم نسبيًا للعلاج غير القلعي، مع ضرورة دمجه ببقية المعطيات."
                        : "منطقة حدّية بين 37% و44%؛ القرار يحتاج تحليلًا سريريًا وسيفالومتريًا كاملًا.");
            warnings.Add("تحليل Ashley Howe مساعد تخطيطي ولا يُستخدم منفردًا لاتخاذ قرار الخلع.");
        }

        MoyersResult? moyers = null;
        TanakaJohnstonResult? tanakaJohnston = null;
        if (lowerIncisors is > 0)
        {
            var percentile = MoyersUpper.ContainsKey(input.MoyersPercentile)
                ? input.MoyersPercentile
                : 75;
            var upperPrediction = InterpolateMoyers(lowerIncisors.Value, MoyersUpper[percentile]);
            var lowerPrediction = InterpolateMoyers(lowerIncisors.Value, MoyersLower[percentile]);
            if (upperPrediction.HasValue && lowerPrediction.HasValue)
            {
                moyers = new MoyersResult(
                    percentile,
                    Round(lowerIncisors.Value),
                    BuildMixedPrediction(
                        upperPrediction.Value,
                        lowerPrediction.Value,
                        input.MixedUpperAvailablePerSide,
                        input.MixedLowerAvailablePerSide));
            }
            else
            {
                warnings.Add("مجموع القواطع السفلية خارج نطاق جدول Moyers المتاح (19.5-25.0 مم).");
            }

            var tjUpper = Round(lowerIncisors.Value / 2m + 11m);
            var tjLower = Round(lowerIncisors.Value / 2m + 10.5m);
            tanakaJohnston = new TanakaJohnstonResult(
                Round(lowerIncisors.Value),
                BuildMixedPrediction(
                    tjUpper,
                    tjLower,
                    input.MixedUpperAvailablePerSide,
                    input.MixedLowerAvailablePerSide));
            warnings.Add("تنبؤا Moyers وTanaka-Johnston يتأثران بالعمر والجنس والمجتمع؛ قارنهما بالقياس الشعاعي عند الشك.");
        }

        var huckaba = input.HuckabaTeeth
            .Where(x => x.RadiographicUneruptedWidth > 0 &&
                        x.ActualReferenceWidth > 0 &&
                        x.RadiographicReferenceWidth > 0)
            .Select(x => new HuckabaToothResult(
                x.ToothCode,
                Round(x.RadiographicUneruptedWidth * x.ActualReferenceWidth / x.RadiographicReferenceWidth)))
            .ToList();

        return new DentalModelAnalysisResult(
            bolton,
            upperArch,
            lowerArch,
            pont,
            howe,
            moyers,
            tanakaJohnston,
            huckaba,
            warnings.Distinct().ToList());
    }

    private static decimal? SumIfComplete(
        IReadOnlyDictionary<string, decimal?> widths,
        IEnumerable<string> keys)
    {
        decimal total = 0;
        foreach (var key in keys)
        {
            if (!widths.TryGetValue(key, out var value) || value is null or <= 0)
                return null;
            total += value.Value;
        }

        return total;
    }

    private static decimal BoltonDiscrepancy(decimal upper, decimal lower, decimal idealRatio)
        => lower / upper >= idealRatio
            ? lower - upper * idealRatio
            : -(upper - lower / idealRatio);

    private static string InterpretBolton(decimal discrepancy)
        => discrepancy > 0.25m
            ? $"زيادة نسبية في الأسنان السفلية بنحو {Math.Abs(discrepancy):0.00} مم"
            : discrepancy < -0.25m
                ? $"زيادة نسبية في الأسنان العلوية بنحو {Math.Abs(discrepancy):0.00} مم"
                : "التناسب قريب من القيمة المرجعية";

    private static ArchSpaceResult? BuildArchResult(decimal? required, decimal? available)
    {
        if (required is null or <= 0 || available is null or <= 0) return null;
        var discrepancy = Round(available.Value - required.Value);
        return new ArchSpaceResult(
            Round(required.Value),
            Round(available.Value),
            discrepancy,
            discrepancy < -0.25m
                ? $"ازدحام بمقدار {Math.Abs(discrepancy):0.00} مم"
                : discrepancy > 0.25m
                    ? $"فراغ متاح بمقدار {discrepancy:0.00} مم"
                    : "المسافة المتاحة والمطلوبة متقاربتان");
    }

    private static MixedDentitionPrediction BuildMixedPrediction(
        decimal upper,
        decimal lower,
        decimal? upperAvailable,
        decimal? lowerAvailable)
        => new(
            Round(upper),
            Round(lower),
            upperAvailable.HasValue ? Round(upperAvailable.Value - upper) : null,
            lowerAvailable.HasValue ? Round(lowerAvailable.Value - lower) : null);

    private static decimal? InterpolateMoyers(decimal lowerIncisorSum, decimal[] values)
    {
        if (lowerIncisorSum < MoyersIncisorSums[0] ||
            lowerIncisorSum > MoyersIncisorSums[^1])
            return null;

        for (var index = 0; index < MoyersIncisorSums.Length; index++)
        {
            if (lowerIncisorSum == MoyersIncisorSums[index])
                return values[index];
            if (index == MoyersIncisorSums.Length - 1 ||
                lowerIncisorSum >= MoyersIncisorSums[index + 1])
                continue;

            var fraction =
                (lowerIncisorSum - MoyersIncisorSums[index]) /
                (MoyersIncisorSums[index + 1] - MoyersIncisorSums[index]);
            return Round(values[index] + (values[index + 1] - values[index]) * fraction);
        }

        return null;
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

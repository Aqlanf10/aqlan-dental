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

// ─── QA-599: New analyses ported from the Aqlan Ortho Model Analysis Android app ──

/// <summary>Arch Perimeter / Carey's Analysis — space available vs required.</summary>
public sealed record ArchPerimeterResult(
    decimal SpaceAvailable,
    decimal SpaceRequired,
    decimal Discrepancy,
    string Diagnosis,
    string Comment);

/// <summary>Ashley Howe Analysis — basal arch width as % of TTM, 44% threshold.</summary>
public sealed record AshleyHoweResult(
    decimal BasalArchPercent,
    string Interpretation,
    string ExpansionPossibility);

/// <summary>Linder Harth Analysis — like Pont but with 85/65 coefficients instead of 80/64.</summary>
public sealed record LinderHarthResult(
    decimal IncisorSum,
    decimal PredictedInterpremolarWidth,
    decimal PredictedIntermolarWidth,
    decimal? MeasuredInterpremolarWidth,
    decimal? MeasuredIntermolarWidth,
    decimal? PremolarDifference,
    decimal? MolarDifference,
    string PremolarDiagnosis,
    string MolarDiagnosis);

/// <summary>Peck & Peck Index — MD/FL ratio for lower incisors (88-92% central, 90-95% lateral).</summary>
public sealed record PeckPeckToothResult(
    string ToothName,
    decimal MdWidth,
    decimal FlWidth,
    decimal Index,
    string Diagnosis);

public sealed record PeckPeckResult(List<PeckPeckToothResult> Teeth);

/// <summary>Korkhaus Analysis — predicted arch length from sum of 4 upper incisors.</summary>
public sealed record KorkhausResult(
    decimal IncisorSum,
    decimal PredictedArchLength,
    decimal? MeasuredArchLength,
    decimal? Difference,
    string Diagnosis);

/// <summary>Nance Mixed Dentition — available vs required space per arch.</summary>
public sealed record NanceMixedResult(
    decimal? MaxAvailable,
    decimal? MaxRequired,
    decimal? MaxDiscrepancy,
    string MaxDiagnosis,
    decimal? MandAvailable,
    decimal? MandRequired,
    decimal? MandDiscrepancy,
    string MandDiagnosis);

/// <summary>Extended result including all QA-599 analyses.</summary>
public sealed record DentalModelAnalysisResultExtended(
    DentalModelAnalysisResult Base,
    ArchPerimeterResult? ArchPerimeter,
    ArchPerimeterResult? Careys,
    AshleyHoweResult? AshleyHowe,
    LinderHarthResult? LinderHarth,
    PeckPeckResult? PeckPeck,
    KorkhausResult? Korkhaus,
    NanceMixedResult? NanceMixed);

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

    // ═══════════════════════════════════════════════════════════════════════════
    // QA-599: New analyses ported from the Aqlan Ortho Model Analysis Android app
    // All formulas and Arabic diagnostic comments are ported verbatim from
    // Calculations.kt to ensure clinical consistency between platforms.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Arch Perimeter / Carey's Analysis — space available vs required.
    /// Discrepancy = available − required. Negative = crowding, positive = spacing.
    /// </summary>
    public static ArchPerimeterResult? CalculateArchPerimeter(decimal? spaceAvailable, decimal? spaceRequired)
    {
        if (spaceAvailable is null or <= 0 || spaceRequired is null or <= 0) return null;
        var discrepancy = Round(spaceAvailable.Value - spaceRequired.Value);
        var diagnosis = discrepancy < 0
            ? "تزاحم (Crowding)"
            : discrepancy > 0
                ? "فراغات (Spacing)"
                : "متطابق (Normal / Adequate Space)";
        var comment = discrepancy < -5.0m
            ? "تزاحم شديد (Severe Crowding). قد يتطلب قلع ضواحك (Extraction of first premolars) بالتنسيق مع رأي الأخصائي."
            : discrepancy < -2.5m
                ? "تزاحم متوسط (Borderline Crowding). قد يتطلب توسيعاً أو قلعاً لضواحك ثانية، أو دراسة مسافة لي واي."
                : discrepancy < 0m
                    ? "تزاحم خفيف (Mild Crowding). يمكن معالجته عبر السنفرة البينية (IPR / Proximal stripping) أو توسيع بسيط."
                    : discrepancy > 0m
                        ? "تفارق خفيف إلى متوسط للأسنان. يتطلب إرجاع القواطع أو إغلاق الفراغات تجميلياً."
                        : "القوس مثالي ومطابق للمادة السنية.";
        return new ArchPerimeterResult(
            Round(spaceAvailable.Value),
            Round(spaceRequired.Value),
            discrepancy,
            diagnosis,
            comment);
    }

    /// <summary>
    /// Ashley Howe Analysis — basal arch width as % of TTM.
    /// PMBAW / TTM × 100. If &lt; 44% → basal arch deficiency. If PMBAW > PMD → expansion possible.
    /// </summary>
    public static AshleyHoweResult? CalculateAshleyHowe(decimal? ttm, decimal? pmd, decimal? pmbaw)
    {
        if (ttm is null or <= 0 || pmd is null || pmbaw is null) return null;
        var pct = Round((pmbaw.Value / ttm.Value) * 100m);
        var expansionPossible = pmbaw.Value > pmd.Value
            ? "التوسيع مفضل وممكن (Expansion is possible / favorable)"
            : "التوسيع محدود أو غير ملائم (Expansion is limited / unfavorable)";
        var interpretation = pct < 44m
            ? "نقص في عرض قاعدة الفك (Basal arch width deficiency - أقل من 44%)."
            : "نطاق عرض قاعدة الفك مناسب ومقبول (Arch width acceptable - 44% أو أكثر).";
        return new AshleyHoweResult(pct, interpretation, expansionPossible);
    }

    /// <summary>
    /// Linder Harth Analysis — like Pont but with 85/65 coefficients instead of 80/64.
    /// CPV = SI × 100 / 85, CMV = SI × 100 / 65.
    /// </summary>
    public static LinderHarthResult? CalculateLinderHarth(
        decimal? si, decimal? measuredPmv, decimal? measuredMv)
    {
        if (si is null or <= 0) return null;
        var cpv = Round(si.Value * 100m / 85m);
        var cmv = Round(si.Value * 100m / 65m);
        var pmDiff = measuredPmv.HasValue ? Round(cpv - measuredPmv.Value) : null;
        var mDiff = measuredMv.HasValue ? Round(cmv - measuredMv.Value) : null;

        var pmDiagnosis = pmDiff switch
        {
            > 1.0m => $"تضيق ليندر-هارث في الضواحك، يحتاج توسعة: {pmDiff.Value:0.0} مم.",
            < -1.0m => $"كفاية أو زيادة في مقاس الضواحك بـ {Math.Abs(pmDiff.Value):0.0} مم.",
            _ => "عرض الضواحك مثالي طبقاً للقواطع."
        };
        var mDiagnosis = mDiff switch
        {
            > 1.0m => $"تضيق ليندر-هارث في الأرحاء، يحتاج توسعة: {mDiff.Value:0.0} مم.",
            < -1.0m => $"كفاية وزيادة في مقاس الأرحاء بـ {Math.Abs(mDiff.Value):0.0} مم.",
            _ => "عرض الأرحاء مثالي طبقاً للقواطع."
        };

        return new LinderHarthResult(
            Round(si.Value), cpv, cmv,
            measuredPmv, measuredMv, pmDiff, mDiff, pmDiagnosis, mDiagnosis);
    }

    /// <summary>
    /// Peck & Peck Index — MD/FL ratio for lower incisors.
    /// Central incisor normal: 88-92%, Lateral incisor normal: 90-95%.
    /// </summary>
    public static PeckPeckResult? CalculatePeckPeck(
        decimal? md31, decimal? fl31,
        decimal? md32, decimal? fl32,
        decimal? md41, decimal? fl41,
        decimal? md42, decimal? fl42)
    {
        var teeth = new List<PeckPeckToothResult>();
        AddPeckTooth(teeth, "القاطع المركزي السفلي الأيسر (31)", md31, fl31, 91m);
        AddPeckTooth(teeth, "القاطع الجانبي السفلي الأيسر (32)", md32, fl32, 94m);
        AddPeckTooth(teeth, "القاطع المركزي السفلي الأيمن (41)", md41, fl41, 91m);
        AddPeckTooth(teeth, "القاطع الجانبي السفلي الأيمن (42)", md42, fl42, 94m);
        return teeth.Count == 0 ? null : new PeckPeckResult(teeth);
    }

    private static void AddPeckTooth(
        List<PeckPeckToothResult> teeth, string name,
        decimal? md, decimal? fl, decimal limit)
    {
        if (md is null or <= 0 || fl is null or <= 0) return;
        var idx = Round((md.Value / fl.Value) * 100m);
        var diag = idx > limit
            ? "المقاس MD عريض جداً بالنسبة لـ FL. ننصح بإجراء سنفرة بينية (Slenderization/IPR) لتناسق الشكل."
            : "أبعاد السن متناسقة تجميلياً ووظيفياً.";
        teeth.Add(new PeckPeckToothResult(name, Round(md.Value), Round(fl.Value), idx, diag));
    }

    /// <summary>
    /// Korkhaus Analysis — predicted arch length from sum of 4 upper incisors.
    /// Predicted = SI × 100 / 160 (simplified Korkhaus formula).
    /// </summary>
    public static KorkhausResult? CalculateKorkhaus(decimal? si, decimal? measuredLength)
    {
        if (si is null or <= 0) return null;
        var predicted = Round(si.Value * 100m / 160m);
        var diff = measuredLength.HasValue ? Round(measuredLength.Value - predicted) : null;
        var diagnosis = diff switch
        {
            > 1.0m => $"طول القوس أكبر من المتوقع بـ {diff.Value:0.0} مم — قد يشير إلى تباعد أو بروز.",
            < -1.0m => $"طول القوس أقصر من المتوقع بـ {Math.Abs(diff.Value):0.0} مم — قد يشير إلى تزاحم.",
            _ => "طول القوس مطابق للمتوقع طبقاً لمجموع القواطع."
        };
        return new KorkhausResult(Round(si.Value), predicted, measuredLength, diff, diagnosis);
    }

    /// <summary>
    /// Nance Mixed Dentition — available vs required space per arch.
    /// Discrepancy = available − required. Negative = crowding.
    /// </summary>
    public static NanceMixedResult? CalculateNanceMixed(
        decimal? maxAvailable, decimal? maxRequired,
        decimal? mandAvailable, decimal? mandRequired)
    {
        if (maxAvailable is null && mandAvailable is null) return null;

        decimal? maxDisc = null, mandDisc = null;
        string maxDiag = "", mandDiag = "";

        if (maxAvailable.HasValue && maxRequired.HasValue)
        {
            maxDisc = Round(maxAvailable.Value - maxRequired.Value);
            maxDiag = maxDisc < 0
                ? $"نقص في مساحة الفك العلوي: {maxDisc.Value:0.0} مم"
                : maxDisc > 0
                    ? $"فراغ متاح بالفك العلوي: {maxDisc.Value:0.0} مم"
                    : "مساحة مطابقة للعلوي";
        }

        if (mandAvailable.HasValue && mandRequired.HasValue)
        {
            mandDisc = Round(mandAvailable.Value - mandRequired.Value);
            mandDiag = mandDisc < 0
                ? $"نقص في مساحة الفك السفلي: {mandDisc.Value:0.0} مم"
                : mandDisc > 0
                    ? $"فراغ متاح بالفك السفلي: {mandDisc.Value:0.0} مم"
                    : "مساحة مطابقة للسفلي";
        }

        return new NanceMixedResult(
            maxAvailable, maxRequired, maxDisc, maxDiag,
            mandAvailable, mandRequired, mandDisc, mandDiag);
    }

    /// <summary>
    /// Huckaba radiographic compensation formula: Y1 = X1 × Y2 / X2.
    /// Used to estimate the actual width of an unerupted tooth from a radiograph.
    /// </summary>
    public static decimal? CalculateHuckabaY1(decimal? x1, decimal? x2, decimal? y2)
    {
        if (x1 is null or <= 0 || x2 is null or <= 0 || y2 is null) return null;
        return Round((x1.Value * y2.Value) / x2.Value);
    }

    /// <summary>
    /// QA-599: Calculates all new analyses in one call and wraps them in an
    /// extended result. The caller passes the same input used for the base
    /// calculation; this method extracts the relevant fields and delegates to
    /// each individual calculation method.
    /// </summary>
    public static DentalModelAnalysisResultExtended CalculateExtended(
        DentalModelAnalysisInput input,
        DentalModelAnalysisResult baseResult,
        // QA-599 new input fields (passed separately to avoid breaking the existing input record)
        decimal? ashleyHoweTtm = null,
        decimal? ashleyHowePmd = null,
        decimal? ashleyHowePmbaw = null,
        decimal? linderHarthSi = null,
        decimal? linderHarthMeasuredPmv = null,
        decimal? linderHarthMeasuredMv = null,
        decimal? peckMd31 = null, decimal? peckFl31 = null,
        decimal? peckMd32 = null, decimal? peckFl32 = null,
        decimal? peckMd41 = null, decimal? peckFl41 = null,
        decimal? peckMd42 = null, decimal? peckFl42 = null,
        decimal? korkhausSi = null,
        decimal? korkhausMeasuredLength = null,
        decimal? nanceMaxAvailable = null, decimal? nanceMaxRequired = null,
        decimal? nanceMandAvailable = null, decimal? nanceMandRequired = null,
        decimal? archPerimeterAvailable = null, decimal? archPerimeterRequired = null,
        decimal? careysAvailable = null, decimal? careysRequired = null)
    {
        return new DentalModelAnalysisResultExtended(
            Base: baseResult,
            ArchPerimeter: CalculateArchPerimeter(archPerimeterAvailable, archPerimeterRequired),
            Careys: CalculateArchPerimeter(careysAvailable, careysRequired),
            AshleyHowe: CalculateAshleyHowe(ashleyHoweTtm, ashleyHowePmd, ashleyHowePmbaw),
            LinderHarth: CalculateLinderHarth(linderHarthSi, linderHarthMeasuredPmv, linderHarthMeasuredMv),
            PeckPeck: CalculatePeckPeck(peckMd31, peckFl31, peckMd32, peckFl32, peckMd41, peckFl41, peckMd42, peckFl42),
            Korkhaus: CalculateKorkhaus(korkhausSi, korkhausMeasuredLength),
            NanceMixed: CalculateNanceMixed(nanceMaxAvailable, nanceMaxRequired, nanceMandAvailable, nanceMandRequired));
    }
}

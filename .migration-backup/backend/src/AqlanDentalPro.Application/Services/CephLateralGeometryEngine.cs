namespace AqlanDentalPro.Application.Services;

public sealed record CephGeometryPoint(double X, double Y);

public sealed record CephGeometryMeasurement(
    string Name,
    string Group,
    double Value,
    double Normal,
    double StandardDeviation,
    string Unit);

public static class CephLateralGeometryEngine
{
    public const string Version = "ADP-CEPH-GEOMETRY-v1";

    public static IReadOnlyList<CephGeometryMeasurement> Calculate(
        IReadOnlyDictionary<string, CephGeometryPoint> landmarks,
        double pixelsPerMillimeter,
        IReadOnlyCollection<string> analysisGroups)
    {
        ArgumentNullException.ThrowIfNull(landmarks);
        ArgumentNullException.ThrowIfNull(analysisGroups);

        var groups = analysisGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<CephGeometryMeasurement>();
        var calibrated = double.IsFinite(pixelsPerMillimeter) && pixelsPerMillimeter > 0;

        bool Has(params string[] keys) => keys.All(landmarks.ContainsKey);
        CephGeometryPoint Get(string key) => landmarks[key];
        void Add(string name, string group, double value, double normal, double sd, string unit)
        {
            if (double.IsFinite(value))
                results.Add(new(name, group, Round1(value), normal, sd, unit));
        }

        if (groups.Contains("steiner"))
        {
            if (Has("S", "N", "A")) Add("SNA", "steiner", Angle(Get("S"), Get("N"), Get("A")), 82, 2, "\u00b0");
            if (Has("S", "N", "B")) Add("SNB", "steiner", Angle(Get("S"), Get("N"), Get("B")), 80, 2, "\u00b0");

            var sna = results.Find(item => item.Name == "SNA")?.Value;
            var snb = results.Find(item => item.Name == "SNB")?.Value;
            if (sna.HasValue && snb.HasValue) Add("ANB", "steiner", sna.Value - snb.Value, 2, 1, "\u00b0");

            if (Has("S", "N", "D")) Add("SND", "steiner", Angle(Get("S"), Get("N"), Get("D")), 76, 2, "\u00b0");
            if (Has("U1A", "U1T", "N", "A")) Add("U1-NA_angle", "steiner", AngleBetweenLines(Get("U1A"), Get("U1T"), Get("N"), Get("A")), 22, 2, "\u00b0");
            if (calibrated && Has("U1T", "N", "A")) Add("U1-NA_mm", "steiner", SignedPerpendicular(Get("U1T"), Get("N"), Get("A")) / pixelsPerMillimeter, 4, 2, "mm");
            if (Has("L1A", "L1T", "N", "B")) Add("L1-NB_angle", "steiner", AngleBetweenLines(Get("L1A"), Get("L1T"), Get("N"), Get("B")), 25, 2, "\u00b0");
            if (calibrated && Has("L1T", "N", "B")) Add("L1-NB_mm", "steiner", SignedPerpendicular(Get("L1T"), Get("N"), Get("B")) / pixelsPerMillimeter, 4, 2, "mm");
            if (Has("U1A", "U1T", "L1A", "L1T")) Add("U1-L1", "steiner", 180 - AngleBetweenLines(Get("U1A"), Get("U1T"), Get("L1A"), Get("L1T")), 131, 6, "\u00b0");
            if (Has("Go", "Me", "S", "N")) Add("GoGn-SN", "steiner", AngleBetweenLines(Get("Go"), Get("Me"), Get("S"), Get("N")), 32, 6, "\u00b0");

            if (Has("Cm", "Pn"))
            {
                var pogKey = Has("SPog") ? "SPog" : "Pog";
                var midpoint = new CephGeometryPoint((Get("Cm").X + Get("Pn").X) / 2, (Get("Cm").Y + Get("Pn").Y) / 2);
                if (calibrated && Has("LS", pogKey)) Add("UL-SLine", "steiner", SignedPerpendicular(Get("LS"), Get(pogKey), midpoint) / pixelsPerMillimeter, 0, 1, "mm");
                if (calibrated && Has("LI", pogKey)) Add("LL-SLine", "steiner", SignedPerpendicular(Get("LI"), Get(pogKey), midpoint) / pixelsPerMillimeter, 0, 1, "mm");
            }
        }

        if (groups.Contains("tweed"))
        {
            if (Has("Po", "Or", "Go", "Me")) Add("FMA", "tweed", AngleBetweenLines(Get("Po"), Get("Or"), Get("Go"), Get("Me")), 25, 4, "\u00b0");
            if (Has("Po", "Or", "L1A", "L1T")) Add("FMIA", "tweed", AngleBetweenLines(Get("Po"), Get("Or"), Get("L1A"), Get("L1T")), 65, 5, "\u00b0");
            if (Has("Go", "Me", "L1A", "L1T")) Add("IMPA", "tweed", AngleBetweenLines(Get("Go"), Get("Me"), Get("L1A"), Get("L1T")), 90, 4, "\u00b0");
        }

        if (groups.Contains("mcnamara"))
        {
            if (calibrated && Has("Co", "A")) Add("Co-A", "mcnamara", Distance(Get("Co"), Get("A")) / pixelsPerMillimeter, 91, 6, "mm");
            if (calibrated && Has("Co", "Gn")) Add("Co-Gn", "mcnamara", Distance(Get("Co"), Get("Gn")) / pixelsPerMillimeter, 120, 7, "mm");
            if (calibrated && Has("ANS", "Me")) Add("ANS-Me", "mcnamara", Distance(Get("ANS"), Get("Me")) / pixelsPerMillimeter, 65, 5, "mm");
        }

        if (groups.Contains("ricketts"))
        {
            if (Has("Po", "Or", "N", "Pog")) Add("Facial-Depth", "ricketts", AngleBetweenLines(Get("Po"), Get("Or"), Get("N"), Get("Pog")), 87, 3, "\u00b0");
            if (Has("S", "N", "Gn")) Add("Facial-Axis", "ricketts", AngleBetweenLines(Get("S"), Get("N"), Get("N"), Get("Gn")), 90, 3, "\u00b0");
            if (Has("Po", "Or", "Go", "Me")) Add("Mandibular-Plane", "ricketts", AngleBetweenLines(Get("Po"), Get("Or"), Get("Go"), Get("Me")), 26, 4, "\u00b0");
            if (calibrated && Has("N", "A", "Pog")) Add("Convexity-A", "ricketts", SignedPerpendicular(Get("A"), Get("N"), Get("Pog")) / pixelsPerMillimeter, 2, 2, "mm");
            if (calibrated && Has("L1T", "A", "Pog")) Add("L1-APog_mm", "ricketts", SignedPerpendicular(Get("L1T"), Get("A"), Get("Pog")) / pixelsPerMillimeter, 1, 2, "mm");
            if (Has("L1A", "L1T", "A", "Pog")) Add("L1-APog_angle", "ricketts", AngleBetweenLines(Get("L1A"), Get("L1T"), Get("A"), Get("Pog")), 22, 4, "\u00b0");
            var pogKey = Has("SPog") ? "SPog" : "Pog";
            if (calibrated && Has("LS", "Pn", pogKey)) Add("Upper-Lip-ELine", "ricketts", SignedPerpendicular(Get("LS"), Get("Pn"), Get(pogKey)) / pixelsPerMillimeter, -2, 2, "mm");
            if (calibrated && Has("LI", "Pn", pogKey)) Add("Lower-Lip-ELine", "ricketts", SignedPerpendicular(Get("LI"), Get("Pn"), Get(pogKey)) / pixelsPerMillimeter, -2, 2, "mm");
            if (Has("Pn", "Cm", "LS")) Add("Nasolabial", "ricketts", Angle(Get("Pn"), Get("Cm"), Get("LS")), 102, 8, "\u00b0");
        }

        if (groups.Contains("downs"))
        {
            if (Has("N", "A", "Pog")) Add("Convexity", "downs", Angle(Get("N"), Get("A"), Get("Pog")) - 180, 0, 5, "\u00b0");
            if (Has("A", "B", "N", "Pog")) Add("AB-FacialPlane", "downs", -AngleBetweenLines(Get("A"), Get("B"), Get("N"), Get("Pog")), -4.6, 3, "\u00b0");
            if (Has("S", "Gn", "Po", "Or")) Add("Y-Axis", "downs", AngleBetweenLines(Get("S"), Get("Gn"), Get("Po"), Get("Or")), 59.4, 4, "\u00b0");
            if (Has("N", "Pog", "Po", "Or")) Add("Facial-Plane-FH", "downs", AngleBetweenLines(Get("N"), Get("Pog"), Get("Po"), Get("Or")), 87.8, 3, "\u00b0");
            if (Has("Go", "Me", "Po", "Or")) Add("Mandibular-FH", "downs", AngleBetweenLines(Get("Go"), Get("Me"), Get("Po"), Get("Or")), 21.9, 4, "\u00b0");
        }

        if (groups.Contains("jarabak"))
        {
            var saddle = Has("N", "S", "Ar") ? Round1(Angle(Get("N"), Get("S"), Get("Ar"))) : (double?)null;
            var articular = Has("S", "Ar", "Go") ? Round1(Angle(Get("S"), Get("Ar"), Get("Go"))) : (double?)null;
            var gonial = Has("Ar", "Go", "Me") ? Round1(Angle(Get("Ar"), Get("Go"), Get("Me"))) : (double?)null;
            if (saddle.HasValue) Add("Saddle-Angle", "jarabak", saddle.Value, 123, 5, "\u00b0");
            if (articular.HasValue) Add("Articular-Angle", "jarabak", articular.Value, 143, 6, "\u00b0");
            if (gonial.HasValue) Add("Gonial-Angle", "jarabak", gonial.Value, 130, 7, "\u00b0");
            if (saddle.HasValue && articular.HasValue && gonial.HasValue) Add("Bjork-Sum", "jarabak", saddle.Value + articular.Value + gonial.Value, 396, 6, "\u00b0");
            if (Has("S", "Go", "N", "Me"))
            {
                var anteriorHeight = Distance(Get("N"), Get("Me"));
                if (anteriorHeight > Epsilon) Add("Jarabak-Ratio", "jarabak", Distance(Get("S"), Get("Go")) / anteriorHeight * 100, 64, 4, "%");
            }
        }

        if (groups.Contains("wits") && calibrated && Has("U1T", "L1T", "A", "B") && (Has("U6", "L6") || Has("Go", "Me")))
            AddWits(results, landmarks, pixelsPerMillimeter);

        return results;
    }

    public static double Angle(CephGeometryPoint p1, CephGeometryPoint vertex, CephGeometryPoint p3)
    {
        var ax = p1.X - vertex.X;
        var ay = p1.Y - vertex.Y;
        var bx = p3.X - vertex.X;
        var by = p3.Y - vertex.Y;
        var magnitudeA = Math.Sqrt(ax * ax + ay * ay);
        var magnitudeB = Math.Sqrt(bx * bx + by * by);
        if (magnitudeA < Epsilon || magnitudeB < Epsilon) return 0;
        return Math.Acos(Math.Clamp((ax * bx + ay * by) / (magnitudeA * magnitudeB), -1, 1)) * 180 / Math.PI;
    }

    public static double AngleBetweenLines(CephGeometryPoint a1, CephGeometryPoint a2, CephGeometryPoint b1, CephGeometryPoint b2)
    {
        var adx = a2.X - a1.X;
        var ady = a2.Y - a1.Y;
        var bdx = b2.X - b1.X;
        var bdy = b2.Y - b1.Y;
        var magnitudeA = Math.Sqrt(adx * adx + ady * ady);
        var magnitudeB = Math.Sqrt(bdx * bdx + bdy * bdy);
        if (magnitudeA < Epsilon || magnitudeB < Epsilon) return 0;
        var cosine = Math.Clamp(Math.Abs(adx * bdx + ady * bdy) / (magnitudeA * magnitudeB), 0, 1);
        return Math.Acos(cosine) * 180 / Math.PI;
    }

    public static double SignedPerpendicular(CephGeometryPoint point, CephGeometryPoint lineStart, CephGeometryPoint lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < Epsilon) return 0;
        return ((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
    }

    public static double Distance(CephGeometryPoint a, CephGeometryPoint b) =>
        Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static void AddWits(
        ICollection<CephGeometryMeasurement> results,
        IReadOnlyDictionary<string, CephGeometryPoint> landmarks,
        double pixelsPerMillimeter)
    {
        CephGeometryPoint Get(string key) => landmarks[key];
        var first = new CephGeometryPoint((Get("U1T").X + Get("L1T").X) / 2, (Get("U1T").Y + Get("L1T").Y) / 2);
        var second = landmarks.ContainsKey("U6") && landmarks.ContainsKey("L6")
            ? new CephGeometryPoint((Get("U6").X + Get("L6").X) / 2, (Get("U6").Y + Get("L6").Y) / 2)
            : new CephGeometryPoint((Get("Go").X + Get("Me").X) / 2, (Get("Go").Y + Get("Me").Y) / 2);
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < Epsilon) return;

        CephGeometryPoint Project(CephGeometryPoint point)
        {
            var t = ((point.X - first.X) * dx + (point.Y - first.Y) * dy) / lengthSquared;
            return new(first.X + t * dx, first.Y + t * dy);
        }

        var ao = Project(Get("A"));
        var bo = Project(Get("B"));
        var length = Math.Sqrt(lengthSquared);
        var value = ((ao.X - bo.X) * dx / length + (ao.Y - bo.Y) * dy / length) / pixelsPerMillimeter;
        results.Add(new("Wits", "wits", Round1(value), 0, 1.5, "mm"));
    }

    private static double Round1(double value) => Math.Round(value, 1);
    private const double Epsilon = 1e-9;
}

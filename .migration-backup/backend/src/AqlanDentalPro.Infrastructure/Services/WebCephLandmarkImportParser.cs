using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed record WebCephLandmarkImportRow(
    string SourceName,
    string LandmarkKey,
    double XMm,
    double YMm);

public sealed record WebCephLandmarkImportDocument(
    string? RecordingDate,
    IReadOnlyList<WebCephLandmarkImportRow> Landmarks);

/// <summary>
/// Parses WebCeph's Landmark Table export. WebCeph names the download .xls,
/// but the current export is a UTF-8 HTML table fragment rather than a binary
/// Excel workbook. Parsing the XML-shaped table keeps patient data local and
/// avoids guessing at cell positions outside the documented three-column rows.
/// </summary>
public static partial class WebCephLandmarkImportParser
{
    public const int MaxFileBytes = 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> CanonicalKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A-point"] = "A",
            ["ANS"] = "ANS",
            ["Ar"] = "Ar",
            ["B-point"] = "B",
            ["Co"] = "Co",
            ["Columella"] = "Cm",
            ["Gn"] = "Gn",
            ["Go"] = "Go",
            ["L1IncisalTip"] = "L1T",
            ["L1RootTip"] = "L1A",
            ["L6Mesial"] = "L6",
            ["LabraleInferius"] = "LI",
            ["LabraleSuperius"] = "LS",
            ["Me"] = "Me",
            ["Na"] = "N",
            ["Or"] = "Or",
            ["PM"] = "Pm",
            ["PNS"] = "PNS",
            ["Po"] = "Po",
            ["Pog"] = "Pog",
            ["Prn"] = "Pn",
            ["S"] = "S",
            ["SoftTissuePog"] = "SPog",
            ["U1IncisalTip"] = "U1T",
            ["U1RootTip"] = "U1A",
            ["U6Mesial"] = "U6",
        };

    public static WebCephLandmarkImportDocument Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.CanSeek && (stream.Length <= 0 || stream.Length > MaxFileBytes))
            throw new InvalidDataException("WebCeph landmark file is empty or exceeds 1 MB.");

        using var reader = new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var html = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(html) || html.Length > MaxFileBytes)
            throw new InvalidDataException("WebCeph landmark file is empty or exceeds 1 MB.");

        html = html.TrimStart('\uFEFF')
            .Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase);

        XDocument document;
        try
        {
            document = XDocument.Parse($"<root>{html}</root>", LoadOptions.None);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The file is not a valid WebCeph Landmark Table export.", ex);
        }

        var recordingDate = document.Root!
            .Descendants("td")
            .Select(CellText)
            .FirstOrDefault(value => IsoDateRegex().IsMatch(value));

        var result = new List<WebCephLandmarkImportRow>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in document.Root.Elements("tr"))
        {
            var cells = row.Descendants("td")
                .Where(cell => HasClass(cell, "td-body"))
                .Select(CellText)
                .ToArray();
            if (cells.Length != 3) continue;

            var sourceName = cells[0];
            if (string.IsNullOrWhiteSpace(sourceName)
                || !TryParseMillimetres(cells[1], out var xMm)
                || !TryParseMillimetres(cells[2], out var yMm))
            {
                continue;
            }

            var baseKey = CanonicalKeys.GetValueOrDefault(sourceName)
                ?? BuildExternalKey(sourceName);
            var key = baseKey;
            for (var suffix = 2; !usedKeys.Add(key); suffix++)
                key = $"{baseKey}_{suffix}";

            result.Add(new WebCephLandmarkImportRow(sourceName, key, xMm, yMm));
        }

        if (result.Count == 0)
            throw new InvalidDataException(
                "No landmark coordinate rows were found in the WebCeph export.");

        return new WebCephLandmarkImportDocument(recordingDate, result);
    }

    private static bool HasClass(XElement element, string className) =>
        (element.Attribute("class")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(className, StringComparer.OrdinalIgnoreCase);

    private static string CellText(XElement element) =>
        string.Join(" ", element.DescendantNodesAndSelf().OfType<XText>().Select(node => node.Value))
            .Replace('\u00A0', ' ')
            .Trim();

    private static bool TryParseMillimetres(string value, out double millimetres)
    {
        millimetres = 0;
        var match = NumberRegex().Match(value);
        return match.Success
            && double.TryParse(
                match.Value,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out millimetres)
            && double.IsFinite(millimetres);
    }

    private static string BuildExternalKey(string sourceName)
    {
        var token = NonAlphaNumericRegex().Replace(sourceName, "_").Trim('_');
        if (token.Length == 0) token = "Landmark";
        if (token.Length > 70) token = token[..70];
        return $"WC_{token}";
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();
}

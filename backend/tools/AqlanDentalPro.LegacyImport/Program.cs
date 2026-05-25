using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;

const string sourceSystem = "Dent2026";
const string executionConfirmation = "LEGACY_ARCHIVE_IMPORT";

var arguments = ParseArguments(args);
if (!arguments.TryGetValue("patients", out var patientsPath)
    || !arguments.TryGetValue("treatments", out var treatmentsPath)
    || !arguments.TryGetValue("journals", out var journalsPath)
    || !arguments.TryGetValue("appointments", out var appointmentsPath)
    || !arguments.TryGetValue("linked-records", out var linkedRecordsPath))
{
    PrintUsage();
    return 2;
}

var patientRows = ReadCsv(patientsPath!);
var treatmentRows = ReadCsv(treatmentsPath!);
var journalRows = ReadCsv(journalsPath!);
var appointmentRows = ReadCsv(appointmentsPath!);
var linkedRecordRows = ReadCsv(linkedRecordsPath!);
var sourceDuplicateFileGroups = patientRows
    .Where(x => !string.IsNullOrWhiteSpace(x["LegacyFileNumber"]))
    .GroupBy(x => x["LegacyFileNumber"])
    .Count(g => g.Count() > 1);

Console.WriteLine($"Source patients: {patientRows.Count}");
Console.WriteLine($"Source treatment lines: {treatmentRows.Count}");
Console.WriteLine($"Source financial reference lines: {journalRows.Count}");
Console.WriteLine($"Source appointment cards: {appointmentRows.Count}");
Console.WriteLine($"Source unclassified linked reference lines: {linkedRecordRows.Count}");
Console.WriteLine($"Duplicate legacy file-number groups preserved for review: {sourceDuplicateFileGroups}");

if (arguments.ContainsKey("source-summary"))
{
    Console.WriteLine("Source-only validation complete. No target database was opened.");
    return 0;
}

var connectionEnvironmentName = arguments.GetValueOrDefault("connection-env") ?? "TARGET_DB_CONNECTION_STRING";
var connectionString = Environment.GetEnvironmentVariable(connectionEnvironmentName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Missing target connection string environment variable: {connectionEnvironmentName}");
    Console.Error.WriteLine("No target database was changed.");
    return 3;
}

if (!Guid.TryParse(arguments.GetValueOrDefault("branch-id"), out var branchId))
{
    Console.Error.WriteLine("A valid --branch-id is required for patient import.");
    return 4;
}

var execute = arguments.ContainsKey("execute");
if (execute && arguments.GetValueOrDefault("confirm") != executionConfirmation)
{
    Console.Error.WriteLine($"Execution refused. Use --confirm {executionConfirmation} only after approving the dry-run report.");
    return 5;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;
await using var db = new AppDbContext(options);

if (!await db.Database.CanConnectAsync())
{
    Console.Error.WriteLine("Cannot connect to target database.");
    return 6;
}

var branchExists = await db.Branches.IgnoreQueryFilters().AnyAsync(x => x.Id == branchId);
if (!branchExists)
{
    Console.Error.WriteLine("Selected target branch does not exist.");
    return 7;
}

try
{
    await db.Patients.IgnoreQueryFilters().Select(x => x.LegacySourceId).Take(1).ToListAsync();
    await db.LegacyTreatmentArchives.Select(x => x.SourceLineId).Take(1).ToListAsync();
    await db.LegacyFinancialArchiveEntries.Select(x => x.SourceEntryId).Take(1).ToListAsync();
    await db.LegacyAppointmentArchives.Select(x => x.SourceAppointmentId).Take(1).ToListAsync();
    await db.LegacyLinkedArchiveRecords.Select(x => x.SourceRecordId).Take(1).ToListAsync();
}
catch (Exception)
{
    Console.Error.WriteLine("Target database does not contain the legacy-import schema migration. Deploy schema changes first.");
    return 8;
}

var importedPatientsBySource = await db.Patients.IgnoreQueryFilters()
    .Where(x => x.LegacySourceId != null)
    .ToDictionaryAsync(x => x.LegacySourceId!, x => x);
var existingNormalizedPhones = await db.Patients.IgnoreQueryFilters()
    .Where(x => x.NormalizedPhone != null && x.NormalizedPhone != "")
    .ToDictionaryAsync(x => x.NormalizedPhone!, x => x.Id);
var existingNames = (await db.Patients.IgnoreQueryFilters()
        .Select(x => x.LegacyFullName ?? (x.FirstName + " " + (x.MiddleName ?? "") + " " + x.LastName))
        .ToListAsync())
    .Select(NormalizeName)
    .Where(x => x.Length > 0)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var sourcePhoneCounts = patientRows
    .Select(x => PhoneNormalizer.Normalize(x["PreferredPhone"]))
    .Where(x => x != null)
    .GroupBy(x => x!)
    .ToDictionary(g => g.Key, g => g.Count());

var proposedPatients = new List<Patient>();
var patientMap = new Dictionary<string, Patient>(StringComparer.OrdinalIgnoreCase);
var targetPhoneConflicts = 0;
var duplicateSourcePhones = 0;
var targetExactNameMatches = 0;

foreach (var row in patientRows)
{
    var sourceId = row["LegacyPatientId"];
    if (importedPatientsBySource.TryGetValue(sourceId, out var imported))
    {
        patientMap[sourceId] = imported;
        continue;
    }

    var normalizedPhone = PhoneNormalizer.Normalize(row["PreferredPhone"]);
    var operationalPhoneAllowed = normalizedPhone != null
        && sourcePhoneCounts[normalizedPhone] == 1
        && !existingNormalizedPhones.ContainsKey(normalizedPhone);

    if (normalizedPhone != null && sourcePhoneCounts[normalizedPhone] > 1)
        duplicateSourcePhones++;
    if (normalizedPhone != null && existingNormalizedPhones.ContainsKey(normalizedPhone))
        targetPhoneConflicts++;
    if (existingNames.Contains(NormalizeName(row["FullName"])))
        targetExactNameMatches++;

    var (firstName, lastName) = MapRequiredName(row["FullName"]);
    var patient = new Patient
    {
        PatientNumber = CreateSystemPatientNumber(sourceId),
        LegacyFileNumber = NullIfEmpty(row["LegacyFileNumber"]),
        LegacySourceId = sourceId,
        LegacyFullName = NullIfEmpty(row["FullName"]),
        LegacyPhone = NullIfEmpty(row["Phone"]),
        LegacyPhone2 = NullIfEmpty(row["Phone2"]),
        LegacyMobile = NullIfEmpty(row["Mobile"]),
        FirstName = firstName,
        LastName = lastName,
        Gender = ParseGender(row["GenderForTarget"]),
        Phone = operationalPhoneAllowed ? NullIfEmpty(row["PreferredPhone"]) : null,
        NormalizedPhone = operationalPhoneAllowed ? normalizedPhone : null,
        Address = NullIfEmpty(row["FullAddress"]),
        BranchId = branchId,
        ReferralSource = "Legacy Dent2026 import"
    };
    proposedPatients.Add(patient);
    patientMap[sourceId] = patient;
}

Console.WriteLine($"Already imported patients: {patientRows.Count - proposedPatients.Count}");
Console.WriteLine($"Patients proposed for import: {proposedPatients.Count}");
Console.WriteLine($"Source rows with duplicated operational phone withheld from Phone field: {duplicateSourcePhones}");
Console.WriteLine($"Potential target phone matches requiring manual review: {targetPhoneConflicts}");
Console.WriteLine($"Potential exact target name matches requiring manual review: {targetExactNameMatches}");

if (targetPhoneConflicts > 0 || targetExactNameMatches > 0)
{
    Console.Error.WriteLine("Import execution blocked: potential existing patients require manual matching first.");
    Console.Error.WriteLine("No target database was changed.");
    return 9;
}

var existingTreatmentIds = (await db.LegacyTreatmentArchives
    .Where(x => x.SourceSystem == sourceSystem)
    .Select(x => x.SourceLineId)
    .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
var existingJournalIds = (await db.LegacyFinancialArchiveEntries
    .Where(x => x.SourceSystem == sourceSystem)
    .Select(x => x.SourceEntryId)
    .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
var existingAppointmentIds = (await db.LegacyAppointmentArchives
    .Where(x => x.SourceSystem == sourceSystem)
    .Select(x => x.SourceAppointmentId)
    .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
var existingLinkedRecordIds = (await db.LegacyLinkedArchiveRecords
    .Where(x => x.SourceSystem == sourceSystem && x.SourceTable == "TBL092")
    .Select(x => x.SourceRecordId)
    .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

var proposedTreatments = treatmentRows
    .Where(row => !existingTreatmentIds.Contains(row["LegacyTreatmentLineId"])
        && patientMap.ContainsKey(row["LegacyPatientId"]))
    .Select(row => new LegacyTreatmentArchive
    {
        Patient = patientMap[row["LegacyPatientId"]],
        SourceSystem = sourceSystem,
        SourceLineId = row["LegacyTreatmentLineId"],
        SourceDocumentId = NullIfEmpty(row["LegacyTreatmentDocumentId"]),
        LegacyFileNumber = NullIfEmpty(row["LegacyFileNumber"]),
        TreatmentDate = ParseDate(row["TreatmentDate"]),
        DocumentType = NullIfEmpty(row["LegacyDocumentType"]),
        ServiceName = NullIfEmpty(row["ServiceName"]),
        Description = NullIfEmpty(row["LineDescription"]),
        LineTotal = ParseDecimal(row["LineTotal"]),
        DiscountAmount = ParseDecimal(row["LineDiscount"]),
        DoctorName = NullIfEmpty(row["LegacyDoctor"]),
        IsOrthodonticService = row["IsOrthodonticService"] == "1"
    })
    .ToList();

var proposedFinancialEntries = journalRows
    .Where(row => !existingJournalIds.Contains(row["LegacyJournalEntryId"])
        && patientMap.ContainsKey(row["LegacyPatientId"]))
    .Select(row => new LegacyFinancialArchiveEntry
    {
        Patient = patientMap[row["LegacyPatientId"]],
        SourceSystem = sourceSystem,
        SourceEntryId = row["LegacyJournalEntryId"],
        LegacyFileNumber = NullIfEmpty(row["LegacyFileNumber"]),
        EntryDate = ParseDate(row["EntryDate"]),
        AccountName = NullIfEmpty(row["AccountName"]),
        Description = NullIfEmpty(row["Description"]),
        DebitAmount = ParseDecimal(row["DebitAmount"]),
        CreditAmount = ParseDecimal(row["CreditAmount"]),
        SourceDocumentId = NullIfEmpty(row["LegacyRelatedDocumentId"]),
        ReconciliationStatus = "ReferenceOnly"
    })
    .ToList();

var proposedAppointments = appointmentRows
    .Where(row => !existingAppointmentIds.Contains(row["LegacyAppointmentId"])
        && patientMap.ContainsKey(row["LegacyPatientId"]))
    .Select(row => new LegacyAppointmentArchive
    {
        Patient = patientMap[row["LegacyPatientId"]],
        SourceSystem = sourceSystem,
        SourceAppointmentId = row["LegacyAppointmentId"],
        LegacyFileNumber = NullIfEmpty(row["LegacyFileNumber"]),
        AppointmentAt = ParseDateTime(row["AppointmentAt"]),
        ArchiveType = NullIfEmpty(row["ArchiveType"]),
        Description = NullIfEmpty(row["Description"]),
        Notes = NullIfEmpty(row["Notes"])
    })
    .ToList();

var proposedLinkedRecords = linkedRecordRows
    .Where(row => !existingLinkedRecordIds.Contains(row["LegacyLinkedRecordId"])
        && patientMap.ContainsKey(row["LegacyPatientId"]))
    .Select(row => new LegacyLinkedArchiveRecord
    {
        Patient = patientMap[row["LegacyPatientId"]],
        SourceSystem = sourceSystem,
        SourceTable = "TBL092",
        SourceRecordId = row["LegacyLinkedRecordId"],
        Classification = "UnmappedReference",
        LegacyFileNumber = NullIfEmpty(row["LegacyFileNumber"]),
        LegacyTypeId = ParseNullableInt(row["LegacyTypeId"]),
        DateValue01 = ParseDateTime(row["DateValue01"]),
        DateValue02 = ParseDateTime(row["DateValue02"]),
        NumberValue01 = ParseNullableDecimal(row["NumberValue01"]),
        AccountName = NullIfEmpty(row["AccountName"]),
        Notes = NullIfEmpty(row["Notes"])
    })
    .ToList();

Console.WriteLine($"Treatment archive rows proposed: {proposedTreatments.Count}");
Console.WriteLine($"Financial reference rows proposed: {proposedFinancialEntries.Count}");
Console.WriteLine($"Appointment archive rows proposed: {proposedAppointments.Count}");
Console.WriteLine($"Unclassified linked reference rows proposed: {proposedLinkedRecords.Count}");
Console.WriteLine("Live payments/contracts/invoices proposed: 0");
Console.WriteLine("Live appointments/queue items proposed: 0");

if (!execute)
{
    Console.WriteLine("DRY RUN ONLY: no target database changes were made.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
db.Patients.AddRange(proposedPatients);
db.LegacyTreatmentArchives.AddRange(proposedTreatments);
db.LegacyFinancialArchiveEntries.AddRange(proposedFinancialEntries);
db.LegacyAppointmentArchives.AddRange(proposedAppointments);
db.LegacyLinkedArchiveRecords.AddRange(proposedLinkedRecords);
await db.SaveChangesAsync();
await transaction.CommitAsync();

Console.WriteLine($"IMPORT COMPLETE: patients={proposedPatients.Count}, treatments={proposedTreatments.Count}, financialReferenceLines={proposedFinancialEntries.Count}, appointmentCards={proposedAppointments.Count}, linkedReferences={proposedLinkedRecords.Count}.");
Console.WriteLine("No live appointment, queue item, payment, contract, invoice, or portal account was created by this tool.");
return 0;

static Dictionary<string, string?> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index++)
    {
        var item = values[index];
        if (!item.StartsWith("--", StringComparison.Ordinal))
            continue;
        var key = item[2..];
        var value = index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? values[++index]
            : null;
        parsed[key] = value;
    }
    return parsed;
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException("Import CSV was not found.", path);

    using var parser = new TextFieldParser(path);
    parser.SetDelimiters(",");
    parser.HasFieldsEnclosedInQuotes = true;
    var headers = parser.ReadFields() ?? throw new InvalidOperationException("CSV has no headers.");
    var rows = new List<Dictionary<string, string>>();
    while (!parser.EndOfData)
    {
        var fields = parser.ReadFields() ?? [];
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Length; index++)
            row[headers[index]] = index < fields.Length ? fields[index] : string.Empty;
        rows.Add(row);
    }
    return rows;
}

static string? NullIfEmpty(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static string NormalizeName(string? value) =>
    string.Join(" ", (value ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Trim()
        .ToUpperInvariant();

static string CreateSystemPatientNumber(string sourceId)
{
    var normalized = sourceId.Replace("-", "", StringComparison.Ordinal);
    return $"OLD-{normalized[..Math.Min(16, normalized.Length)]}";
}

static (string FirstName, string LastName) MapRequiredName(string name)
{
    var cleaned = string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    if (string.IsNullOrWhiteSpace(cleaned))
        return ("مريض", "قديم");
    var tokens = cleaned.Split(' ');
    if (tokens.Length == 1)
        return (Limit(tokens[0]), ".");
    return (Limit(string.Join(" ", tokens[..^1])), Limit(tokens[^1]));
}

static string Limit(string value) => value.Length > 100 ? value[..100] : value;

static Gender? ParseGender(string value) => value switch
{
    "Male" => Gender.Male,
    "Female" => Gender.Female,
    _ => null
};

static DateOnly? ParseDate(string value) =>
    DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        ? DateOnly.FromDateTime(date)
        : null;

static decimal ParseDecimal(string value) =>
    decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

static decimal? ParseNullableDecimal(string value) =>
    decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : null;

static int? ParseNullableInt(string value) =>
    int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : null;

static DateTime? ParseDateTime(string value) =>
    DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

static void PrintUsage()
{
    Console.WriteLine("Legacy import tool. Dry run is the default and never modifies target data.");
    Console.WriteLine("Source check:");
    Console.WriteLine("  dotnet run -- --source-summary --patients <csv> --treatments <csv> --journals <csv> --appointments <csv> --linked-records <csv>");
    Console.WriteLine("Target dry run:");
    Console.WriteLine("  dotnet run -- --patients <csv> --treatments <csv> --journals <csv> --appointments <csv> --linked-records <csv> --branch-id <guid>");
    Console.WriteLine("Execution requires --execute --confirm LEGACY_ARCHIVE_IMPORT after dry-run review.");
}

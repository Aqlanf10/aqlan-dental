[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "AqlanLegacy_ReadOnly"
)

$ErrorActionPreference = "Stop"

function Invoke-DataTable {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $command = $Connection.CreateCommand()
    $command.CommandTimeout = 120
    $command.CommandText = $Sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Export-PrivateCsv {
    param([System.Data.DataTable]$Table, [string]$Path)
    $Table | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$account = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $OutputRoot /inheritance:r /grant:r "${account}:(OI)(CI)F" /grant:r "SYSTEM:(OI)(CI)F" | Out-Null

$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=10"
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$connection.Open()

try {
    $state = Invoke-DataTable $connection @"
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(30), DATABASEPROPERTYEX(DB_NAME(), 'Updateability')) AS Updateability;
"@
    if ($state.Rows[0].Updateability -ne "READ_ONLY") {
        throw "Source database is not READ_ONLY. Export stopped before reading patient data."
    }

    $patients = Invoke-DataTable $connection @"
WITH DuplicateFileNumbers AS (
    SELECT CardNumber FROM dbo.TBL016
    WHERE NULLIF(LTRIM(RTRIM(CardNumber)), '') IS NOT NULL
    GROUP BY CardNumber HAVING COUNT(*) > 1
)
SELECT CONVERT(nvarchar(36), p.CardGuide) AS LegacyPatientId,
       LTRIM(RTRIM(p.CardNumber)) AS LegacyFileNumber,
       LTRIM(RTRIM(p.AgentName)) AS FullName,
       CASE
           WHEN genderLabel.CardName = NCHAR(1584) + NCHAR(1603) + NCHAR(1585) THEN 'Male'
           WHEN genderLabel.CardName = NCHAR(1575) + NCHAR(1606) + NCHAR(1579) + NCHAR(1609) THEN 'Female'
           ELSE NULL
       END AS GenderForTarget,
       NULLIF(LTRIM(RTRIM(p.Phone)), '') AS Phone,
       NULLIF(LTRIM(RTRIM(p.Phone2)), '') AS Phone2,
       NULLIF(LTRIM(RTRIM(p.Mobile)), '') AS Mobile,
       COALESCE(NULLIF(LTRIM(RTRIM(p.Mobile)), ''), NULLIF(LTRIM(RTRIM(p.Phone)), ''), NULLIF(LTRIM(RTRIM(p.Phone2)), '')) AS PreferredPhone,
       NULLIF(LTRIM(RTRIM(p.EMail)), '') AS Email,
       NULLIF(LTRIM(RTRIM(p.FullAdress)), '') AS FullAddress,
       CASE WHEN d.CardNumber IS NULL THEN 0 ELSE 1 END AS HasDuplicateLegacyFileNumber
FROM dbo.TBL016 p
OUTER APPLY (SELECT TOP (1) c.CardName FROM dbo.TBL081 c WHERE c.CardGuide = p.Category01) genderLabel
LEFT JOIN DuplicateFileNumbers d ON d.CardNumber = p.CardNumber
ORDER BY TRY_CONVERT(int, p.CardNumber), p.CardNumber, p.AgentName;
"@

    $treatments = Invoke-DataTable $connection @"
SELECT CONVERT(nvarchar(36), h.CardGuide) AS LegacyTreatmentDocumentId,
       CONVERT(nvarchar(36), h.AgentGuide) AS LegacyPatientId,
       LTRIM(RTRIM(p.CardNumber)) AS LegacyFileNumber,
       h.BillDate AS TreatmentDate,
       doc.InvoiceName AS LegacyDocumentType,
       CONVERT(nvarchar(36), l.RowGuide) AS LegacyTreatmentLineId,
       service.ProductName AS ServiceName,
       l.StatementName AS LineDescription,
       ISNULL(l.TotalValue, 0) AS LineTotal,
       ISNULL(l.DiscountValue, 0) AS LineDiscount,
       CASE WHEN service.ProductName LIKE N'%' + NCHAR(1578) + NCHAR(1602) + NCHAR(1608) + NCHAR(1610) + NCHAR(1605) + N'%'
            THEN 1 ELSE 0 END AS IsOrthodonticService,
       NULLIF(LTRIM(RTRIM(h.Doctor)), '') AS LegacyDoctor
FROM dbo.TBL022 h
INNER JOIN dbo.TBL016 p ON p.CardGuide = h.AgentGuide
LEFT JOIN dbo.TBL020 doc ON doc.CardGuide = h.MainGuide
INNER JOIN dbo.TBL023 l ON l.MainGuide = h.CardGuide
LEFT JOIN dbo.TBL007 service ON service.CardGuide = l.ProductGuide
ORDER BY h.BillDate, p.CardNumber, h.CardGuide, l.ID;
"@

    $journals = Invoke-DataTable $connection @"
SELECT CONVERT(nvarchar(36), d.RowGuide) AS LegacyJournalEntryId,
       CONVERT(nvarchar(36), d.AgentGuide) AS LegacyPatientId,
       p.CardNumber AS LegacyFileNumber,
       d.RowDate AS EntryDate,
       account.AccountName,
       d.Description,
       ISNULL(d.Debit, 0) AS DebitAmount,
       ISNULL(d.Credit, 0) AS CreditAmount,
       CONVERT(nvarchar(36), h.BillGuide) AS LegacyRelatedDocumentId
FROM dbo.TBL012 d
INNER JOIN dbo.TBL016 p ON p.CardGuide = d.AgentGuide
LEFT JOIN dbo.TBL004 account ON account.CardGuide = d.AccountGuide
LEFT JOIN dbo.TBL011 h ON h.CardGuide = d.MainGuide
ORDER BY d.RowDate, p.CardNumber, d.ID;
"@

    $appointments = Invoke-DataTable $connection @"
SELECT CONVERT(nvarchar(36), a.CardGuide) AS LegacyAppointmentId,
       CONVERT(nvarchar(36), a.AgentGuide) AS LegacyPatientId,
       LTRIM(RTRIM(p.CardNumber)) AS LegacyFileNumber,
       a.CardDate AS AppointmentAt,
       t.CardName AS ArchiveType,
       NULLIF(LTRIM(RTRIM(a.TextValue01)), '') AS Description,
       NULLIF(LTRIM(RTRIM(a.Notes)), '') AS Notes
FROM dbo.TBL085 a
INNER JOIN dbo.TBL016 p ON p.CardGuide = a.AgentGuide
LEFT JOIN dbo.TBL084 t ON t.CardGuide = a.TypeGuide
ORDER BY a.CardDate, p.CardNumber, a.CardGuide;
"@

    $linkedRecords = Invoke-DataTable $connection @"
SELECT CONCAT('TBL092:', CONVERT(nvarchar(20), x.ID)) AS LegacyLinkedRecordId,
       CONVERT(nvarchar(36), x.AgentGuide) AS LegacyPatientId,
       LTRIM(RTRIM(p.CardNumber)) AS LegacyFileNumber,
       x.TypeID AS LegacyTypeId,
       x.DateValue01,
       x.DateValue02,
       x.NumberValue01,
       account.AccountName,
       NULLIF(LTRIM(RTRIM(x.Notes)), '') AS Notes
FROM dbo.TBL092 x
INNER JOIN dbo.TBL016 p ON p.CardGuide = x.AgentGuide
LEFT JOIN dbo.TBL004 account ON account.CardGuide = x.AccountGuide
ORDER BY p.CardNumber, x.ID;
"@

    $binarySummary = Invoke-DataTable $connection @"
SELECT
    (SELECT COUNT(*) FROM dbo.TBL016 WHERE CardImage IS NOT NULL AND DATALENGTH(CardImage) > 0)
    + (SELECT COUNT(*) FROM dbo.TBL016 WHERE CardImage2 IS NOT NULL AND DATALENGTH(CardImage2) > 0)
    + (SELECT COUNT(*) FROM dbo.TBL022 WHERE CardImage IS NOT NULL AND DATALENGTH(CardImage) > 0)
    + (SELECT COUNT(*) FROM dbo.TBL085 WHERE CardImage IS NOT NULL AND DATALENGTH(CardImage) > 0)
    + (SELECT COUNT(*) FROM dbo.TBL092 WHERE CardImage IS NOT NULL AND DATALENGTH(CardImage) > 0)
    + (SELECT COUNT(*) FROM dbo.TBL092 WHERE Attachment IS NOT NULL AND DATALENGTH(Attachment) > 0)
      AS NonEmptyBinaryFieldsFound;
"@

    Export-PrivateCsv $patients (Join-Path $OutputRoot "legacy-patients-private.csv")
    Export-PrivateCsv $treatments (Join-Path $OutputRoot "legacy-treatment-lines-private.csv")
    Export-PrivateCsv $journals (Join-Path $OutputRoot "legacy-journal-entries-private.csv")
    Export-PrivateCsv $appointments (Join-Path $OutputRoot "legacy-appointment-cards-private.csv")
    Export-PrivateCsv $linkedRecords (Join-Path $OutputRoot "legacy-linked-reference-records-private.csv")

    $summary = [ordered]@{
        sourceDatabase = $state.Rows[0].DatabaseName
        sourceDatabaseMode = $state.Rows[0].Updateability
        patients = $patients.Rows.Count
        duplicateLegacyFileNumberGroups = @($patients | Where-Object { $_.HasDuplicateLegacyFileNumber -eq 1 } | Group-Object LegacyFileNumber).Count
        treatmentLines = $treatments.Rows.Count
        journalEntryLines = $journals.Rows.Count
        appointmentCards = $appointments.Rows.Count
        linkedReferenceRecords = $linkedRecords.Rows.Count
        nonEmptyLegacyImageOrAttachmentFieldsFound = $binarySummary.Rows[0].NonEmptyBinaryFieldsFound
        rules = @(
            "CSV exports contain patient-identifying data and must never be committed.",
            "Financial records are reference only until separately reconciled.",
            "Appointment cards are historical only and must not enter the live schedule."
        )
    }
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot "summary-safe.json") -Encoding UTF8
    $summary | ConvertTo-Json -Depth 4
}
finally {
    $connection.Close()
}

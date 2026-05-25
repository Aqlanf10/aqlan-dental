# Dent2026 Legacy Archive Import

This tool imports historical patient archive records from the desktop Dent2026 database without creating live financial or operational activity.

## Safety

- Restore the source SQL Server backup to a separate database and set it to `READ_ONLY` first.
- Store extracted CSV files outside the repository in an access-restricted folder.
- Never commit backup files, CSV exports, connection strings, credentials, or target dry-run output containing patient matches.
- Legacy journal rows are reference-only. They do not create payments, invoices, contracts, patient balances, or ledger transactions.
- Legacy appointment cards are historical. They do not create live appointments or queue records.

## Extract From Read-Only SQL Server Source

```powershell
.\Export-Dent2026Archive.ps1 `
  -Server ".\SQLEXPRESS" `
  -Database "AqlanLegacy_ReadOnly" `
  -OutputRoot "C:\private\aqlan-legacy-export"
```

The script refuses to read patient data unless the source database is `READ_ONLY`.

## Validate Source Files Only

```powershell
dotnet run --configuration Release -- `
  --source-summary `
  --patients "C:\private\aqlan-legacy-export\legacy-patients-private.csv" `
  --treatments "C:\private\aqlan-legacy-export\legacy-treatment-lines-private.csv" `
  --journals "C:\private\aqlan-legacy-export\legacy-journal-entries-private.csv" `
  --appointments "C:\private\aqlan-legacy-export\legacy-appointment-cards-private.csv" `
  --linked-records "C:\private\aqlan-legacy-export\legacy-linked-reference-records-private.csv"
```

This opens no target database.

## Target Dry Run

After the additive archive migrations are deployed to the target environment, set the target connection string only in the local shell environment and run without `--execute`:

```powershell
$env:TARGET_DB_CONNECTION_STRING = "<secure target connection string>"
dotnet run --configuration Release -- `
  --patients "C:\private\aqlan-legacy-export\legacy-patients-private.csv" `
  --treatments "C:\private\aqlan-legacy-export\legacy-treatment-lines-private.csv" `
  --journals "C:\private\aqlan-legacy-export\legacy-journal-entries-private.csv" `
  --appointments "C:\private\aqlan-legacy-export\legacy-appointment-cards-private.csv" `
  --linked-records "C:\private\aqlan-legacy-export\legacy-linked-reference-records-private.csv" `
  --branch-id "<target branch id>"
```

The dry run blocks execution when an existing target phone or exact patient name requires manual reconciliation.

## Execute

Execution must only occur after review of the dry-run report and explicit approval:

```powershell
dotnet run --configuration Release -- <same dry-run arguments> --execute --confirm LEGACY_ARCHIVE_IMPORT
```

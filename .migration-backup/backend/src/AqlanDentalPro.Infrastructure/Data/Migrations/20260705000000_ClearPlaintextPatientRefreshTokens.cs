using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// SEC-09 — Invalidate all pre-existing plaintext patient refresh tokens.
///
/// Background:
///   Before SEC-09, PatientAccounts.RefreshToken stored the PLAINTEXT refresh
///   token issued to portal clients. A read-only DB compromise (backup leak,
///   SQL injection, insider dump) was enough to impersonate every portal user
///   for up to 30 days.
///
/// SEC-09 fix (separate code change) renames the C# property to RefreshTokenHash
/// while keeping the DB column name "RefreshToken" via [Column("RefreshToken")]
/// — no schema migration is needed. From this release forward, the column
/// stores SHA-256(plaintext) as lowercase hex, never the plaintext itself.
///
/// This migration is a one-time hygiene pass that NULLs out every pre-existing
/// value in the column. Those values are plaintext tokens issued under the old
/// scheme; under the new scheme they will never hash-match an incoming refresh
/// request, so leaving them in place would be useless to legitimate users and
/// would keep a plaintext trove live in the DB for an attacker. NULLing them
/// forces every portal user to log in once on their next visit — an acceptable
/// one-time cost for a security fix. The new login will write a fresh SHA-256
/// hash under the same column.
///
/// Idempotent: re-running on an already-clean DB is a no-op (UPDATE sets NULL
/// on rows that are already NULL).
///
/// Down: no-op. We cannot recover the original plaintext tokens, and we would
/// not want to even if we could.
/// </summary>
public partial class ClearPlaintextPatientRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // NULL out any pre-existing plaintext refresh tokens. Under the new
        // hash-based scheme these would never match an incoming request anyway,
        // so we are simply removing the plaintext residue from the DB.
        migrationBuilder.Sql(@"
            UPDATE ""PatientAccounts""
               SET ""RefreshToken"" = NULL
             WHERE ""RefreshToken"" IS NOT NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: we cannot and would not restore the old
        // plaintext tokens.
    }
}

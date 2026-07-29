using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// LAB-FINANCE-ROOT-CAUSE: migration 20260618000000_FixEnumColumnTypesPhase1 converted
/// "SupplierBills"."Status" from integer to varchar(20) via a raw ALTER COLUMN ... TYPE,
/// but never added a column-level DEFAULT — even though SupplierBillConfiguration has
/// always declared .HasDefaultValue(BillStatus.Unpaid) and the model snapshot has always
/// recorded that default as already applied.
///
/// EF Core's SaveChanges omits a property from the generated INSERT whenever its current
/// value equals the CLR default of its type AND the property is configured with a
/// database-generated default — trusting the database to supply it instead. BillStatus.Unpaid
/// is exactly the CLR default (enum value 0), and it is also the status of every single new
/// SupplierBill (a lab order is always billed "Unpaid" the moment it is first sent). So every
/// first-time INSERT into "SupplierBills" omitted the "Status" column outright. Since the
/// column had no real default in the database, Postgres rejected it with
/// `null value in column "Status" of relation "SupplierBills" violates not-null constraint" —
/// a 500 on every lab order's very first transition to "sent". This is the confirmed root
/// cause of lab orders never reaching Accounts Payable / Finance V3 / profitability /
/// commission: the record that should carry the cost across never got created in the first
/// place, on a real Postgres database (EF's InMemory test provider does not reproduce this
/// column-omission behavior against a real schema, which is why the existing unit test suite
/// never caught it).
///
/// Idempotent — ALTER COLUMN ... SET DEFAULT is safe to run repeatedly and does not touch
/// existing rows.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729070000_FixSupplierBillStatusColumnDefault")]
public partial class FixSupplierBillStatusColumnDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "SupplierBills" ALTER COLUMN "Status" SET DEFAULT 'Unpaid';
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "SupplierBills" ALTER COLUMN "Status" DROP DEFAULT;
        """);
    }
}

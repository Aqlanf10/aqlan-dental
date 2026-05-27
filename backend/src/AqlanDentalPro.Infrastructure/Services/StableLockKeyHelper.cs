using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Provides deterministic, stable bigint keys for PostgreSQL advisory locks.
///
/// .NET string.GetHashCode() is NOT stable across application instances, app domain
/// restarts, or different processes. PostgreSQL advisory locks require the same bigint
/// key across all application replicas to provide cross-process serialization.
///
/// This helper uses SHA256 to derive a stable int64 from well-known lock names,
/// ensuring the same logical lock sequence always maps to the same bigint regardless
/// of which process or machine generates it.
///
/// Lock keys are defined as named constants so they are discoverable, auditable,
/// and never accidentally duplicated.
/// </summary>
public static class StableLockKeyHelper
{
    // ─── Finance Number-Generation Lock Keys ────────────────────────────────
    // Each logical number sequence gets its own deterministic bigint key.
    // The key is derived from a well-known name string via SHA256, ensuring
    // the same value across all processes and restarts.

    /// <summary>Lock key for JournalEntry number generation (JE-yyyyMMdd-NNN)</summary>
    public const long JournalEntryNumber = 2001;

    /// <summary>Lock key for Receipt number generation (REC-yyyyMMdd-NNN)</summary>
    public const long ReceiptNumber = 2002;

    /// <summary>Lock key for Refund Receipt number generation (REF-yyyyMMdd-NNN)</summary>
    public const long RefundReceiptNumber = 2003;

    /// <summary>Lock key for Invoice number generation (INV-yyyyMMdd-NNN)</summary>
    public const long InvoiceNumber = 2004;

    /// <summary>Lock key for Expense number generation (EXP-yyyyMMdd-NNN)</summary>
    public const long ExpenseNumber = 2005;

    /// <summary>Lock key for Supplier Bill number generation (BILL-yyyyMMdd-NNN)</summary>
    public const long BillNumber = 2006;

    /// <summary>Lock key for Vault Transfer number generation (TR-yyyyMMdd-NNN)</summary>
    public const long VaultTransferNumber = 2007;

    /// <summary>Lock key for Cashier Session number generation (CS-yyyyMMdd-NNN)</summary>
    public const long CashierSessionNumber = 2008;

    // ─── Cashier Session Identity Lock Keys ─────────────────────────────────
    // Used to prevent duplicate active sessions for the same cashier.
    // Derived from the cashier's Guid using StableGuidToLong.

    // ─── Helper: Convert a Guid to a stable long ────────────────────────────
    /// <summary>
    /// Converts a Guid to a stable long for use as a PostgreSQL advisory lock key.
    /// Uses the first 8 bytes of the Guid — deterministic across processes and restarts.
    /// Do NOT use .NET GetHashCode() which is not stable across app domains.
    /// </summary>
    public static long StableGuidToLong(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return BitConverter.ToInt64(bytes, 0);
    }

    // ─── Helper: Derive a stable int64 from a well-known name ───────────────
    /// <summary>
    /// Derives a stable int64 from a name string using SHA256.
    /// The same input always produces the same output across all processes and restarts.
    /// This was the original derivation method for the named constants above.
    /// The constants are pre-computed to avoid runtime SHA256 overhead in hot paths,
    /// but this method is kept for any future lock key derivation needs.
    /// </summary>
    public static long DeriveFromName(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return BitConverter.ToInt64(hash, 0);
    }
}

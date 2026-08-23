using System.Reflection;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.GoLive;

/// <summary>
/// CORE-REQ-006 — the print language must actually change the printed document.
///
/// <para>
/// The setting, the Settings screen field and the four English identity keys all existed and
/// were editable. Nothing in the PDF-generating layer read any of them, so the switch changed
/// nothing on the documents it was named for — the same shape of defect as the permission
/// switches the roles screen used to show.
/// </para>
///
/// <para>
/// Worse, both defaults said English: <c>NormalizePrintLanguage</c> and the Settings default
/// map. Wiring the reader up without correcting them would have turned every receipt in an
/// Arabic clinic English on deploy. That is what these first tests pin.
/// </para>
/// </summary>
public class PrintLanguageTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    /// <summary>Builds an identity through the private constructor, as ResolveAsync does.</summary>
    private static FinanceClinicIdentity Build(bool printEnglish)
    {
        var ctor = typeof(FinanceClinicIdentity)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single();

        return (FinanceClinicIdentity)ctor.Invoke(
        [
            "مركز الدكتور عقلان الكامل", "د. عقلان الكامل", "أخصائي تقويم الأسنان",
            "جامعة مانيلا المركزية — الفلبين", "04-253028", "تعز، اليمن", "", true,
            "Dr. Aqlan Alkamel Center", "Taiz, Yemen",
            "Dr. Aqlan Alkamel — Orthodontic Specialist",
            "Central University of Manila — Philippines",
            printEnglish,
        ]);
    }

    [Fact]
    public void An_english_print_language_prints_the_english_identity()
    {
        var identity = Build(printEnglish: true);

        identity.PrintsEnglish.Should().BeTrue();
        identity.Name.Should().Be("Dr. Aqlan Alkamel Center");
        identity.Location.Should().Be("Taiz, Yemen");
        identity.LeadDoctor.Should().Be("Dr. Aqlan Alkamel — Orthodontic Specialist");
        identity.LeadDoctorCredentials.Should().Be("Central University of Manila — Philippines");

        // The English doctor setting already carries the title, so printing a separate title
        // line under it repeated it — and left a dangling "—" when the composer added one.
        identity.LeadDoctorTitle.Should().BeEmpty();
        identity.HasLeadDoctorTitle.Should().BeFalse(
            "the Has* flags must answer for the language being printed, not the raw Arabic field");
    }

    [Fact]
    public void Arabic_is_unchanged_by_the_english_keys_being_present()
    {
        var identity = Build(printEnglish: false);

        identity.PrintsEnglish.Should().BeFalse();
        identity.Name.Should().Be("مركز الدكتور عقلان الكامل");
        identity.LeadDoctor.Should().Be("د. عقلان الكامل");
        identity.LeadDoctorTitle.Should().Be("أخصائي تقويم الأسنان");
        identity.HasLeadDoctorTitle.Should().BeTrue();
    }

    [Fact]
    public void An_unset_print_language_means_arabic_not_english()
    {
        var publicController = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Controllers", "PublicController.cs"));
        var settings = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Controllers", "SettingsController.cs"));

        publicController.Length.Should().BeGreaterThan(2_000);

        publicController.Should().Contain("value is \"ar\" or \"en\" ? value : \"ar\"",
            "an unset or unrecognised print language must fall back to the language the clinic prints in");
        settings.Should().Contain("[\"printLanguage\"]            = \"ar\"",
            "English is a deliberate choice, never the default for this clinic");
    }

    /// <summary>
    /// A document that switches its letterhead but keeps an Arabic body is worse for the
    /// reader than either language alone, so the receipt's fixed chrome must go through the
    /// language helper. Patient names and service descriptions are data and stay as entered.
    /// </summary>
    [Fact]
    public void The_receipt_body_follows_the_print_language_too()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.Infrastructure", "Services", "PdfDocuments.cs"));

        var start = source.IndexOf("public class PaymentReceiptDocument(", StringComparison.Ordinal);
        var end = source.IndexOf("public class FinancialStatementDocument(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);

        var receipt = source[start..end];

        foreach (var label in new[]
                 {
                     "سند قبض", "اسم المريض:", "رقم الملف:", "الطبيب المعالج:",
                     "طريقة الدفع:", "المبلغ المقبوض", "توقيع المريض", "ختم المركز",
                     "رقم السند", "التاريخ",
                 })
        {
            receipt.Should().Contain($"L(\"{label}\"",
                $"«{label}» is fixed chrome and must print in the document's language");
        }

        receipt.Should().Contain("if (!Identity.PrintsEnglish)",
            "an English receipt laid out right-to-left puts every label on the wrong side");
    }

    /// <summary>
    /// The statement and the invoice follow the same rule as the receipt. Enforced together
    /// because a half-translated set is the failure this work exists to remove: an English
    /// letterhead over Arabic column headings reads worse than either language alone.
    /// </summary>
    [Fact]
    public void The_statement_and_invoice_follow_the_print_language_as_well()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.Infrastructure", "Services", "PdfDocuments.cs"));

        var statementStart = source.IndexOf("public class FinancialStatementDocument(", StringComparison.Ordinal);
        var invoiceStart = source.IndexOf("public class InvoiceDocument(", StringComparison.Ordinal);
        statementStart.Should().BeGreaterThan(-1);
        invoiceStart.Should().BeGreaterThan(statementStart);

        var statement = source[statementStart..invoiceStart];
        var invoice = source[invoiceStart..];

        foreach (var label in new[] { "التاريخ", "طريقة الدفع", "المبلغ المقبوض", "الرصيد المتبقي المستحق:" })
            statement.Should().Contain($"L(\"{label}\"", $"«{label}» is a statement column heading, not data");

        foreach (var label in new[] { "الكمية", "سعر الوحدة", "المجموع الفرعي:", "الخصم:", "الضريبة:" })
            invoice.Should().Contain($"L(\"{label}\"", $"«{label}» is invoice chrome, not data");

        foreach (var (name, body) in new[] { ("statement", statement), ("invoice", invoice) })
            body.Should().Contain("if (!Identity.PrintsEnglish)",
                $"the {name}'s page direction must follow its print language too");

        // One helper, on the identity every finance document already resolves — three private
        // copies of the same ternary is how they drift apart.
        foreach (var (name, body) in new[] { ("statement", statement), ("invoice", invoice) })
            body.Should().Contain("Identity.T(ar, en)",
                $"the {name} must share the identity's label helper, not reimplement it");
    }
}

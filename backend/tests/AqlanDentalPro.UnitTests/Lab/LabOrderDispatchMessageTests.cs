using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using LabOrderEntity = AqlanDentalPro.Domain.Entities.LabOrder;
using PatientEntity = AqlanDentalPro.Domain.Entities.Patient;

namespace AqlanDentalPro.UnitTests.Lab;

/// <summary>
/// LABINV-REQ-009 — the WhatsApp message sent to a lab when a case goes out.
///
/// <para>
/// Two properties are worth defending. First, clinic identity must come from
/// <c>Settings</c>: a message with the clinic's name written into the source keeps sending
/// the old name after a rename, and this system forbids that class of hardcoding
/// explicitly. Second, what the message does <b>not</b> carry is as deliberate as what it
/// does — the patient's name is switchable, and the cost never goes out at all.
/// </para>
/// </summary>
public class LabOrderDispatchMessageTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<FinanceClinicIdentity> ClinicAsync(
        params (string Key, string Value)[] settings)
    {
        using var db = CreateDb();
        foreach (var (key, value) in settings)
            db.Settings.Add(new Setting { Key = key, Value = value, Category = "clinic" });
        await db.SaveChangesAsync();
        return await FinanceClinicIdentity.ResolveAsync(db);
    }

    private static LabOrderEntity BuildOrder(Action<LabOrderEntity>? tweak = null)
    {
        var order = new LabOrderEntity
        {
            Id = Guid.NewGuid(),
            OrderNumber = "LAB-2026-003",
            ApplianceType = "تاج زيركون",
            Status = "sent",
            Priority = "normal",
            SentDate = new DateOnly(2026, 8, 18),
            ExpectedDate = new DateOnly(2026, 8, 25),
            Cost = 45000m,
            TotalCost = 45000m,
            Currency = "YER",
            Patient = new PatientEntity
            {
                Id = Guid.NewGuid(),
                FirstName = "سالم",
                LastName = "الحكيمي",
                PatientNumber = "GM-2026-065",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
            },
        };
        tweak?.Invoke(order);
        return order;
    }

    private static List<LabOrderItem> Items(params LabOrderItem[] items) => [.. items];

    // ── Clinic identity ───────────────────────────────────────────────────────

    [Fact]
    public async Task Uses_The_Clinic_Name_From_Settings()
    {
        var clinic = await ClinicAsync(("clinic.name", "مركز اختبار الأسنان"));

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, true);

        message.Should().Contain("مركز اختبار الأسنان");
    }

    /// <summary>
    /// The rename test. If the name were a literal in the composer this would fail, which
    /// is precisely why it is written this way rather than asserting the default.
    /// </summary>
    [Fact]
    public async Task A_Renamed_Clinic_Sends_Its_New_Name()
    {
        var before = await ClinicAsync(("clinic.name", "الاسم القديم"));
        var after = await ClinicAsync(("clinic.name", "الاسم الجديد"));

        var order = BuildOrder();
        LabOrderDispatchMessage.Compose(order, Items(), before, true).Should().Contain("الاسم القديم");

        var newMessage = LabOrderDispatchMessage.Compose(order, Items(), after, true);
        newMessage.Should().Contain("الاسم الجديد");
        newMessage.Should().NotContain("الاسم القديم");
    }

    [Fact]
    public async Task Includes_The_Clinic_Phones_From_Settings()
    {
        var clinic = await ClinicAsync(("clinic.phones", "770245745"));

        LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, true)
            .Should().Contain("770245745");
    }

    // ── Order content ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Carries_The_Order_Number_And_Dates()
    {
        var clinic = await ClinicAsync();

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, true);

        message.Should().Contain("LAB-2026-003");
        message.Should().Contain("2026-08-18");
        message.Should().Contain("2026-08-25");
    }

    [Fact]
    public async Task Flags_An_Urgent_Case()
    {
        var clinic = await ClinicAsync();

        var urgent = LabOrderDispatchMessage.Compose(
            BuildOrder(o => o.Priority = "urgent"), Items(), clinic, true);
        var normal = LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, true);

        urgent.Should().Contain("مستعجلة");
        normal.Should().NotContain("مستعجلة");
    }

    [Fact]
    public async Task Lists_Item_Tooth_Shade_And_Count()
    {
        var clinic = await ClinicAsync();
        var items = Items(new LabOrderItem
        {
            SortOrder = 1,
            ToothNumber = "16",
            Shade = "A2",
            UnitsCount = 3,
            WorkType = new LabWorkType { Name = "Zirconia Crown", NameAr = "تاج زيركون" },
        });

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), items, clinic, true);

        message.Should().Contain("تاج زيركون");
        message.Should().Contain("سن 16");
        message.Should().Contain("لون A2");
        message.Should().Contain("عدد 3");
    }

    /// <summary>The message goes to a Yemeni technician; the English name is a catalogue key.</summary>
    [Fact]
    public async Task Prefers_The_Arabic_Work_Type_Name()
    {
        var clinic = await ClinicAsync();
        var items = Items(new LabOrderItem
        {
            SortOrder = 1,
            UnitsCount = 1,
            WorkType = new LabWorkType { Name = "Zirconia Crown", NameAr = "تاج زيركون" },
        });

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), items, clinic, true);

        message.Should().Contain("تاج زيركون");
        message.Should().NotContain("Zirconia Crown");
    }

    [Fact]
    public async Task Falls_Back_To_The_English_Name_When_There_Is_No_Arabic_One()
    {
        var clinic = await ClinicAsync();
        var items = Items(new LabOrderItem
        {
            SortOrder = 1,
            UnitsCount = 1,
            WorkType = new LabWorkType { Name = "Emax Veneer", NameAr = null },
        });

        LabOrderDispatchMessage.Compose(BuildOrder(), items, clinic, true)
            .Should().Contain("Emax Veneer");
    }

    [Fact]
    public async Task Skips_An_Item_That_Has_Nothing_To_Say()
    {
        var clinic = await ClinicAsync();
        var items = Items(new LabOrderItem { SortOrder = 1, UnitsCount = 1 });

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), items, clinic, true);

        message.Should().NotContain("1. ", "an item with no work type, tooth or shade prints nothing useful");
    }

    // ── What the message must NOT carry ───────────────────────────────────────

    [Fact]
    public async Task Omits_The_Patient_Name_When_The_Clinic_Turns_It_Off()
    {
        var clinic = await ClinicAsync();

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, includePatientName: false);

        message.Should().NotContain("سالم");
        message.Should().NotContain("الحكيمي");
        message.Should().Contain("GM-2026-065", "the lab still needs a handle to reply about");
    }

    [Fact]
    public async Task Includes_The_Patient_Name_When_The_Clinic_Allows_It()
    {
        var clinic = await ClinicAsync();

        LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, includePatientName: true)
            .Should().Contain("سالم الحكيمي");
    }

    /// <summary>
    /// Price is agreed in the lab price list. Repeating it on every case turns a work order
    /// into a running quotation sitting in a technician's chat history.
    /// </summary>
    [Fact]
    public async Task Never_Sends_The_Cost()
    {
        var clinic = await ClinicAsync();

        var message = LabOrderDispatchMessage.Compose(BuildOrder(), Items(), clinic, true);

        message.Should().NotContain("45000");
        message.Should().NotContain("التكلفة");
    }

    // ── Robustness ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Omits_Fields_That_Are_Missing_Rather_Than_Printing_Blanks()
    {
        var clinic = await ClinicAsync();
        var bare = BuildOrder(o =>
        {
            o.OrderNumber = null;
            o.ApplianceType = null;
            o.SentDate = null;
            o.ExpectedDate = null;
            o.Instructions = null;
            o.Patient = null;
        });

        var message = LabOrderDispatchMessage.Compose(bare, Items(), clinic, true);

        message.Should().NotContain("رقم الطلب:");
        message.Should().NotContain("المريض:");
        message.Should().NotContain("نوع العمل:");
        message.Should().NotBeNullOrWhiteSpace("the clinic header still identifies the sender");
    }

    [Fact]
    public async Task Appends_Order_Instructions_When_Present()
    {
        var clinic = await ClinicAsync();
        var order = BuildOrder(o => o.Instructions = "يرجى مراعاة الإطباق الأمامي");

        LabOrderDispatchMessage.Compose(order, Items(), clinic, true)
            .Should().Contain("يرجى مراعاة الإطباق الأمامي");
    }
}

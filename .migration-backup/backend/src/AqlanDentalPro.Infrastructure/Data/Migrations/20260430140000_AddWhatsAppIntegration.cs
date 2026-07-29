using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260430140000_AddWhatsAppIntegration")]
public partial class AddWhatsAppIntegration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WhatsAppTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TemplateKey = table.Column<string>(maxLength: 100, nullable: false),
                NameAr = table.Column<string>(maxLength: 200, nullable: false),
                ContentTemplate = table.Column<string>(maxLength: 2000, nullable: false),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                Category = table.Column<string>(maxLength: 50, nullable: false),
                IsActive1 = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WhatsAppMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PatientId = table.Column<Guid>(nullable: false),
                PhoneNumber = table.Column<string>(maxLength: 20, nullable: false),
                TemplateType = table.Column<string>(maxLength: 100, nullable: false),
                MessageContent = table.Column<string>(maxLength: 2000, nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "pending"),
                ExternalId = table.Column<string>(maxLength: 200, nullable: true),
                ErrorMessage = table.Column<string>(maxLength: 500, nullable: true),
                RetryCount = table.Column<int>(nullable: false, defaultValue: 0),
                SentAt = table.Column<DateTime>(nullable: true),
                DeliveredAt = table.Column<DateTime>(nullable: true),
                RelatedEntityId = table.Column<Guid>(nullable: true),
                RelatedEntityType = table.Column<string>(maxLength: 50, nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_WhatsAppMessages_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppTemplates_TemplateKey",
            table: "WhatsAppTemplates",
            column: "TemplateKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessages_PatientId",
            table: "WhatsAppMessages",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessages_Status",
            table: "WhatsAppMessages",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessages_CreatedAt",
            table: "WhatsAppMessages",
            column: "CreatedAt");

        // Seed default templates
        var templateId1 = Guid.NewGuid();
        var templateId2 = Guid.NewGuid();
        var templateId3 = Guid.NewGuid();
        var templateId4 = Guid.NewGuid();
        var templateId5 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        migrationBuilder.InsertData("WhatsAppTemplates", new[] { "Id", "TemplateKey", "NameAr", "ContentTemplate", "IsActive", "Category", "CreatedAt", "UpdatedAt" },
            new object[] { templateId1, "appointment_reminder", "تذكير موعد", "مرحباً {{patient_name}}\n\nنذكركم بموعدكم في مركز د. عقلان الكامل:\n📅 التاريخ: {{date}}\n🕐 الوقت: {{time}}\n🦷 نوع الموعد: {{appointment_type}}\n👨‍⚕️ الطبيب: {{doctor_name}}\n\nيرجى الحضور قبل الموعد بـ10 دقائق.\nلإلغاء أو تعديل الموعد، تواصلوا معنا على: 04-253028", true, "appointment", now, now });

        migrationBuilder.InsertData("WhatsAppTemplates", new[] { "Id", "TemplateKey", "NameAr", "ContentTemplate", "IsActive", "Category", "CreatedAt", "UpdatedAt" },
            new object[] { templateId2, "appointment_confirmation", "تأكيد موعد", "تم تأكيد موعدكم بنجاح ✅\n\n{{patient_name}}\n📅 {{date}} at {{time}}\n🦷 {{appointment_type}}\n👨‍⚕️ {{doctor_name}}\n\nنتطلع لرؤيتكم!", true, "appointment", now, now });

        migrationBuilder.InsertData("WhatsAppTemplates", new[] { "Id", "TemplateKey", "NameAr", "ContentTemplate", "IsActive", "Category", "CreatedAt", "UpdatedAt" },
            new object[] { templateId3, "payment_reminder", "تذكير دفع", "مرحباً {{patient_name}}\n\nنود تذكيركم بأن لديكم رصيد مستحق بقيمة {{amount}} في مركز د. عقلان الكامل.\n\nللاستفسار، تواصلوا معنا على: 04-253028", true, "payment", now, now });

        migrationBuilder.InsertData("WhatsAppTemplates", new[] { "Id", "TemplateKey", "NameAr", "ContentTemplate", "IsActive", "Category", "CreatedAt", "UpdatedAt" },
            new object[] { templateId4, "payment_confirmation", "تأكيد دفع", "تم استلام الدفعة بنجاح ✅\n\n{{patient_name}}\nالمبلغ: {{amount}}\nرقم الإيصال: {{receipt_number}}\n\nشكراً لكم!", true, "payment", now, now });

        migrationBuilder.InsertData("WhatsAppTemplates", new[] { "Id", "TemplateKey", "NameAr", "ContentTemplate", "IsActive", "Category", "CreatedAt", "UpdatedAt" },
            new object[] { templateId5, "welcome", "ترحيب بمريض جديد", "أهلاً وسهلاً {{patient_name}} في مركز د. عقلان الكامل لطب وتقويم الأسنان 🦷\n\nرقم ملفكم: {{patient_number}}\nالطبيب المخصص: {{doctor_name}}\n\nساعات العمل:\nالسبت - الأربعاء: 9 ص - 9 م\nالخميس: 9 ص - 5 م\nالجمعة: مغلق\n\nللحجز والاستفسار: 04-253028", true, "general", now, now });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WhatsAppMessages");
        migrationBuilder.DropTable(name: "WhatsAppTemplates");
    }
}

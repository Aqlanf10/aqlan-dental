from pathlib import Path

# Application abstraction keeps PatientService independent from EF/Infrastructure.
interface = Path("backend/src/AqlanDentalPro.Application/Interfaces/Services/IPatientSettingsReader.cs")
interface.write_text('''namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientSettingsReader
{
    Task<string> GetNumberPrefixAsync(CancellationToken cancellationToken = default);
}
''', encoding="utf-8")

reader = Path("backend/src/AqlanDentalPro.Infrastructure/Services/PatientSettingsReader.cs")
reader.write_text('''using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed class PatientSettingsReader(AppDbContext db) : IPatientSettingsReader
{
    public const string NumberPrefixKey = "patient.number_prefix";
    public const string DefaultNumberPrefix = "GM";
    private const int MaxPrefixLength = 8;

    public async Task<string> GetNumberPrefixAsync(CancellationToken cancellationToken = default)
    {
        var stored = await db.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == NumberPrefixKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return NormalizePrefix(stored);
    }

    internal static string NormalizePrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultNumberPrefix;

        var normalized = new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Take(MaxPrefixLength)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? DefaultNumberPrefix : normalized;
    }
}
''', encoding="utf-8")

# PatientService reads the same database setting exposed in the Admin UI.
path = Path("backend/src/AqlanDentalPro.Application/Services/PatientService.cs")
text = path.read_text(encoding="utf-8")
text = text.replace('''    ICurrentUserService currentUser,
    IConfiguration config,
    IPatientPortalService portalService,
''', '''    ICurrentUserService currentUser,
    IPatientSettingsReader patientSettings,
    IPatientPortalService portalService,
''', 1)
text = text.replace('''    private string NumberPrefix => config["Settings:PatientNumberPrefix"] ?? "GM";

''', '', 1)
text = text.replace('''        var number = await repo.GeneratePatientNumberAsync(NumberPrefix);
''', '''        var numberPrefix = await patientSettings.GetNumberPrefixAsync();
        var number = await repo.GeneratePatientNumberAsync(numberPrefix);
''', 1)
path.write_text(text, encoding="utf-8")

# Register the reader.
path = Path("backend/src/AqlanDentalPro.API/Configuration/ServiceRegistrationConfiguration.cs")
text = path.read_text(encoding="utf-8")
old = '''        services.AddScoped<PatientService>();
        services.AddScoped<AppointmentService>();
'''
new = '''        services.AddScoped<IPatientSettingsReader, PatientSettingsReader>();
        services.AddScoped<PatientService>();
        services.AddScoped<AppointmentService>();
'''
if old not in text: raise SystemExit("DI registration target not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")

# Update PatientService unit construction.
path = Path("backend/tests/AqlanDentalPro.UnitTests/Services/PatientHistoryUpsertTests.cs")
text = path.read_text(encoding="utf-8")
text = text.replace('using Microsoft.Extensions.Configuration;\n', '', 1)
text = text.replace('''    private readonly Mock<IConfiguration> _configMock;
''', '''    private readonly Mock<IPatientSettingsReader> _patientSettingsMock;
''', 1)
text = text.replace('''        _configMock = new Mock<IConfiguration>();
''', '''        _patientSettingsMock = new Mock<IPatientSettingsReader>();
''', 1)
text = text.replace('''        _configMock.Setup(c => c["Settings:PatientNumberPrefix"]).Returns("GM");

        _service = new PatientService(_repoMock.Object, _currentUserMock.Object, _configMock.Object, _portalServiceMock.Object, _loggerMock.Object);
''', '''        _patientSettingsMock.Setup(s => s.GetNumberPrefixAsync(It.IsAny<CancellationToken>())).ReturnsAsync("GM");

        _service = new PatientService(_repoMock.Object, _currentUserMock.Object, _patientSettingsMock.Object, _portalServiceMock.Object, _loggerMock.Object);
''', 1)
path.write_text(text, encoding="utf-8")

path = Path("backend/tests/AqlanDentalPro.UnitTests/BookingRequests/BookingRequestConvertTests.cs")
text = path.read_text(encoding="utf-8")
text = text.replace('using Microsoft.Extensions.Configuration;\n', '', 1)
text = text.replace('''        var config = new Mock<IConfiguration>();
        var portal = new Mock<IPatientPortalService>();
''', '''        var patientSettings = new Mock<IPatientSettingsReader>();
        patientSettings.Setup(s => s.GetNumberPrefixAsync(It.IsAny<CancellationToken>())).ReturnsAsync("GM");
        var portal = new Mock<IPatientPortalService>();
''', 1)
text = text.replace('''            new PatientRepository(db), currentUser.Object, config.Object,
            portal.Object, new Mock<ILogger<PatientService>>().Object);
''', '''            new PatientRepository(db), currentUser.Object, patientSettings.Object,
            portal.Object, new Mock<ILogger<PatientService>>().Object);
''', 1)
path.write_text(text, encoding="utf-8")

# Backend reader tests use the real Settings table and normalization.
test = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientSettingsReaderTests.cs")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class PatientSettingsReaderTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient-settings-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetNumberPrefixAsync_NoStoredValue_ReturnsGM()
    {
        await using var db = CreateDb();
        var reader = new PatientSettingsReader(db);

        (await reader.GetNumberPrefixAsync()).Should().Be("GM");
    }

    [Fact]
    public async Task GetNumberPrefixAsync_UsesTheAdminSetting()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = PatientSettingsReader.NumberPrefixKey,
            Value = "adc",
            Category = "patients",
        });
        await db.SaveChangesAsync();

        var reader = new PatientSettingsReader(db);

        (await reader.GetNumberPrefixAsync()).Should().Be("ADC");
    }

    [Theory]
    [InlineData(" A-C 10 ", "AC10")]
    [InlineData("--------", "GM")]
    [InlineData("LONGPREFIX123", "LONGPREF")]
    public void NormalizePrefix_ProducesAStableSafePrefix(string value, string expected)
    {
        PatientSettingsReader.NormalizePrefix(value).Should().Be(expected);
    }
}
''', encoding="utf-8")

# Admin Clinic tab must actually submit the field already visible in the form.
path = Path("frontend/src/app/(dashboard)/settings/_components/ClinicTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace('''  "patient.number_prefix": z.string().optional(),
''', '''  "patient.number_prefix": z.string()
    .trim()
    .min(1, "بادئة رقم المريض مطلوبة")
    .max(8, "الحد الأقصى 8 أحرف")
    .regex(/^[\\p{L}\\p{N}]+$/u, "استخدم أحرفًا وأرقامًا فقط"),
''', 1)
old = '''      const clinicFields = [
        "clinic.name", "clinic.location", "clinic.phones",
        "clinic.lead_doctor", "clinic.lead_doctor_title", "clinic.lead_doctor_credentials",
      ] as const;
      await Promise.all(
        clinicFields.map((key) =>
          api.put(`/api/settings/${encodeURIComponent(key)}`, {
            value: formData[key] ?? "",
            category: "clinic",
          })
        )
      );
'''
new = '''      const settingsFields = [
        { key: "clinic.name", category: "clinic" },
        { key: "clinic.location", category: "clinic" },
        { key: "clinic.phones", category: "clinic" },
        { key: "clinic.lead_doctor", category: "clinic" },
        { key: "clinic.lead_doctor_title", category: "clinic" },
        { key: "clinic.lead_doctor_credentials", category: "clinic" },
        { key: "patient.number_prefix", category: "patients" },
      ] as const;
      await Promise.all(
        settingsFields.map(({ key, category }) =>
          api.put(`/api/settings/${encodeURIComponent(key)}`, {
            value: formData[key] ?? "",
            category,
          })
        )
      );
'''
if old not in text: raise SystemExit("Clinic settings submit block not found")
text = text.replace(old, new, 1)
text = text.replace('''        <input
          {...register("patient.number_prefix")}
          className={inputCls}
          placeholder="GM"
          dir="ltr"
        />
''', '''        <input
          {...register("patient.number_prefix")}
          className={inputCls}
          placeholder="GM"
          dir="ltr"
          maxLength={8}
        />
        {errors["patient.number_prefix"] && (
          <p className="text-xs text-red-600 mt-1">{errors["patient.number_prefix"].message}</p>
        )}
        <p className="text-xs text-gray-500 mt-1">تُستخدم في أرقام المرضى الجدد فقط، مثل GM-2026-001.</p>
''', 1)
path.write_text(text, encoding="utf-8")

contract = Path("frontend/src/__tests__/settings/PatientNumberPrefixSettings.contract.test.ts")
contract.parent.mkdir(parents=True, exist_ok=True)
contract.write_text('''import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const clinicTab = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/settings/_components/ClinicTab.tsx"),
  "utf8"
);

describe("patient number prefix settings contract", () => {
  it("submits the visible prefix field to the canonical database setting", () => {
    expect(clinicTab).toContain('{ key: "patient.number_prefix", category: "patients" }');
    expect(clinicTab).toContain('api.put(`/api/settings/${encodeURIComponent(key)}`');
  });

  it("validates a short alphanumeric prefix and explains its scope", () => {
    expect(clinicTab).toContain("الحد الأقصى 8 أحرف");
    expect(clinicTab).toContain("استخدم أحرفًا وأرقامًا فقط");
    expect(clinicTab).toContain("تُستخدم في أرقام المرضى الجدد فقط");
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_043_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-043-bootstrap.yml").unlink(missing_ok=True)

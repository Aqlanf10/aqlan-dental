from pathlib import Path

# ── Canonical backend query definition ───────────────────────────────────────
extension = Path("backend/src/AqlanDentalPro.Infrastructure/Data/OrthoCaseQueryExtensions.cs")
extension.write_text('''using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Infrastructure.Data;

/// <summary>
/// Canonical definition of a clinically active orthodontic case.
/// IsActive is the soft-delete flag; Status is the clinical lifecycle.
/// Both must be true so completed/cancelled cases never appear as active.
/// </summary>
public static class OrthoCaseQueryExtensions
{
    public static IQueryable<OrthoCase> ActiveCases(this IQueryable<OrthoCase> query) =>
        query.Where(c => c.IsActive && c.Status == OrthoCaseStatus.Active);
}
''', encoding="utf-8")

# Dashboard counter.
dashboard = Path("backend/src/AqlanDentalPro.Infrastructure/Services/DashboardService.cs")
text = dashboard.read_text(encoding="utf-8")
old = "        var orthoQuery = db.OrthoCases.Where(o => o.Status == OrthoCaseStatus.Active);\n"
new = "        var orthoQuery = db.OrthoCases.ActiveCases();\n"
if old not in text:
    raise SystemExit("CORE-PAT-029: dashboard active ortho query not found")
dashboard.write_text(text.replace(old, new, 1), encoding="utf-8")

# Patient profile summary.
patients = Path("backend/src/AqlanDentalPro.API/Controllers/PatientsController.cs")
text = patients.read_text(encoding="utf-8")
old = '''        var activeOrthoSummary = await db.OrthoCases
            .AsNoTracking()
            .Where(o => o.PatientId == id && o.Status == OrthoCaseStatus.Active)
'''
new = '''        var activeOrthoSummary = await db.OrthoCases
            .AsNoTracking()
            .ActiveCases()
            .Where(o => o.PatientId == id)
'''
if old not in text:
    raise SystemExit("CORE-PAT-029: patient summary active ortho query not found")
patients.write_text(text.replace(old, new, 1), encoding="utf-8")

# Daily journey: one path used soft-delete only; prefetch already had both conditions.
journey = Path("backend/src/AqlanDentalPro.Infrastructure/Services/PatientJourneyService.cs")
text = journey.read_text(encoding="utf-8")
old_daily = '''            var activeOrthoCase = await db.OrthoCases
                .IgnoreQueryFilters()
                .Where(o => o.PatientId == patientId && o.IsActive)
'''
new_daily = '''            var activeOrthoCase = await db.OrthoCases
                .IgnoreQueryFilters()
                .ActiveCases()
                .Where(o => o.PatientId == patientId)
'''
if old_daily not in text:
    raise SystemExit("CORE-PAT-029: daily summary active ortho query not found")
text = text.replace(old_daily, new_daily, 1)
old_prefetch = '''        var cases = await db.OrthoCases
            .IgnoreQueryFilters()
            .Where(c => c.IsActive
                && c.Status == OrthoCaseStatus.Active
                && patientIds.Contains(c.PatientId))
'''
new_prefetch = '''        var cases = await db.OrthoCases
            .IgnoreQueryFilters()
            .ActiveCases()
            .Where(c => patientIds.Contains(c.PatientId))
'''
if old_prefetch not in text:
    raise SystemExit("CORE-PAT-029: journey prefetch active ortho query not found")
journey.write_text(text.replace(old_prefetch, new_prefetch, 1), encoding="utf-8")

# ── Canonical frontend route ──────────────────────────────────────────────────
route = Path("frontend/src/lib/orthoCaseRoutes.ts")
route.write_text('''export function patientActiveOrthoCasesUrl(
  patientId: string,
  pageSize = 100,
): string {
  const params = new URLSearchParams({
    patientId,
    status: "active",
    page: "1",
    pageSize: String(pageSize),
  });
  return `/api/ortho-cases?${params.toString()}`;
}
''', encoding="utf-8")

# Overview: do not fetch five unfiltered cases and then search locally.
overview = Path("frontend/src/components/patient/tabs/OverviewTab.tsx")
text = overview.read_text(encoding="utf-8")
import_anchor = 'import { financeV3CollectionsUrl } from "@/lib/financeRoutes";\n'
if import_anchor not in text:
    raise SystemExit("CORE-PAT-029: Overview import anchor not found")
text = text.replace(import_anchor, import_anchor + 'import { patientActiveOrthoCasesUrl } from "@/lib/orthoCaseRoutes";\n', 1)
old_url = 'api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=5`).then((response) => response.data),'
new_url = 'api.get<OrthoCase[]>(patientActiveOrthoCasesUrl(patientId, 5)).then((response) => response.data),'
if old_url not in text:
    raise SystemExit("CORE-PAT-029: Overview ortho URL not found")
overview.write_text(text.replace(old_url, new_url, 1), encoding="utf-8")

# Ortho-surgical creation: only clinically active ortho cases may be linked.
surgical = Path("frontend/src/components/patient/tabs/OrthoSurgicalTab.tsx")
text = surgical.read_text(encoding="utf-8")
import_anchor = 'import api from "@/lib/api";\n'
if import_anchor not in text:
    raise SystemExit("CORE-PAT-029: OrthoSurgical import anchor not found")
text = text.replace(import_anchor, import_anchor + 'import { patientActiveOrthoCasesUrl } from "@/lib/orthoCaseRoutes";\n', 1)
old_url = 'api.get<OrthoCaseLite[]>(`/api/ortho-cases?patientId=${patientId}`),'
new_url = 'api.get<OrthoCaseLite[]>(patientActiveOrthoCasesUrl(patientId)),'
if old_url not in text:
    raise SystemExit("CORE-PAT-029: OrthoSurgical ortho URL not found")
surgical.write_text(text.replace(old_url, new_url, 1), encoding="utf-8")

# ── Tests ─────────────────────────────────────────────────────────────────────
backend_test = Path("backend/tests/AqlanDentalPro.UnitTests/Ortho/ActiveOrthoCaseDefinitionTests.cs")
backend_test.write_text('''using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class ActiveOrthoCaseDefinitionTests
{
    [Fact]
    public async Task ActiveCases_RequiresBothSoftDeleteAndClinicalActiveStatus()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var patientId = Guid.NewGuid();

        db.OrthoCases.AddRange(
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-ACTIVE",
                Status = OrthoCaseStatus.Active,
                IsActive = true,
            },
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-COMPLETED",
                Status = OrthoCaseStatus.Completed,
                IsActive = true,
            },
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-DELETED",
                Status = OrthoCaseStatus.Active,
                IsActive = false,
            });
        await db.SaveChangesAsync();

        var result = await db.OrthoCases
            .IgnoreQueryFilters()
            .ActiveCases()
            .Select(c => c.CaseNumber)
            .ToListAsync();

        result.Should().Equal("ORTHO-ACTIVE");
    }
}
''', encoding="utf-8")

frontend_test = Path("frontend/src/__tests__/lib/orthoCaseRoutes.test.ts")
frontend_test.write_text('''import { describe, expect, it } from "vitest";
import { patientActiveOrthoCasesUrl } from "@/lib/orthoCaseRoutes";

describe("active orthodontic case routes", () => {
  it("always sends the clinical active status explicitly", () => {
    const url = patientActiveOrthoCasesUrl("patient 1", 5);
    const parsed = new URL(url, "https://clinic.example");

    expect(parsed.pathname).toBe("/api/ortho-cases");
    expect(parsed.searchParams.get("patientId")).toBe("patient 1");
    expect(parsed.searchParams.get("status")).toBe("active");
    expect(parsed.searchParams.get("page")).toBe("1");
    expect(parsed.searchParams.get("pageSize")).toBe("5");
  });

  it("uses a bounded full patient list for surgical linking", () => {
    const parsed = new URL(
      patientActiveOrthoCasesUrl("patient-1"),
      "https://clinic.example",
    );
    expect(parsed.searchParams.get("status")).toBe("active");
    expect(parsed.searchParams.get("pageSize")).toBe("100");
  });
});
''', encoding="utf-8")

# Source contract: all named active-case consumers must use the canonical definition.
source_test = Path("backend/tests/AqlanDentalPro.UnitTests/Ortho/ActiveOrthoCaseConsumerContractTests.cs")
source_test.write_text('''using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class ActiveOrthoCaseConsumerContractTests
{
    [Fact]
    public void PatientSummaryDashboardAndJourney_UseCanonicalActiveCasesQuery()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "backend", "src", "AqlanDentalPro.API", "Controllers", "PatientsController.cs"),
            Path.Combine(root, "backend", "src", "AqlanDentalPro.Infrastructure", "Services", "DashboardService.cs"),
            Path.Combine(root, "backend", "src", "AqlanDentalPro.Infrastructure", "Services", "PatientJourneyService.cs"),
        };

        foreach (var file in files)
            File.ReadAllText(file).Should().Contain(".ActiveCases()", file);

        File.ReadAllText(files[2]).Should().NotContain(
            ".Where(o => o.PatientId == patientId && o.IsActive)",
            "soft-delete alone is not a clinical active-case definition");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "backend", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-029-bootstrap.yml"),
    Path("scripts/core_pat_029_patch.py"),
):
    temporary.unlink(missing_ok=True)

using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// CLIN-10 — Unit tests for the stratified cephalometric norm lookup
/// (CephService.FindBestCephNorm). The lookup must:
///   1. Pick the child (6–12) norm for a child patient (not the adult norm).
///   2. Fall back to an un-stratified row when no stratified match exists.
///   3. Prefer a sex-specific row over a sex-null row for the same age band.
///   4. NEVER match a child (6–12) row for an adult patient (patient safety).
/// </summary>
public class CephNormLookupTests
{
    private static CephNorm Row(
        string measurement, string group, decimal normal,
        int? ageMin = null, int? ageMax = null, string? sex = null) => new()
    {
        Id = Guid.NewGuid(),
        MeasurementName = measurement,
        AnalysisGroup = group,
        NormalValue = normal,
        StdDeviation = 2m,
        Unit = "°",
        IsActive = true,
        AgeMin = ageMin,
        AgeMax = ageMax,
        Sex = sex,
    };

    [Fact]
    public void CephNormLookup_Stratified_PicksChildNormForChildPatient()
    {
        // SNA norms: child 6-12 = 81°, adolescent 13-17 = 81°, adult 18+ = 82°.
        var norms = new List<CephNorm>
        {
            Row("SNA", "steiner", 81, ageMin: 6,  ageMax: 12),
            Row("SNA", "steiner", 81, ageMin: 13, ageMax: 17),
            Row("SNA", "steiner", 82, ageMin: 18, ageMax: null),
        };

        // A 10-year-old must get the CHILD norm (81°), NOT the adult (82°).
        var best = CephService.FindBestCephNorm(norms, "SNA", "steiner", patientAge: 10, patientSex: null);

        best.Should().NotBeNull();
        best!.NormalValue.Should().Be(81m, "a 10-year-old is in the 6–12 child band");
        best.AgeMin.Should().Be(6);
        best.AgeMax.Should().Be(12);

        // Cross-check: a 14-year-old gets the ADOLESCENT norm (81°, distinct row).
        var teen = CephService.FindBestCephNorm(norms, "SNA", "steiner", patientAge: 14, patientSex: null);
        teen.Should().NotBeNull();
        teen!.AgeMin.Should().Be(13);
        teen.AgeMax.Should().Be(17);
    }

    [Fact]
    public void CephNormLookup_FallsBackToUnstratified_WhenNoStratifiedMatch()
    {
        // Convexity (Downs) — left un-stratified (no age band applies). The
        // pre-CLIN-10 behavior: a single row applies to all patients.
        var norms = new List<CephNorm>
        {
            Row("Convexity", "downs", 0m), // AgeMin/AgeMax/Sex all null
        };

        // Regardless of age or sex, the un-stratified row is returned.
        var forChild = CephService.FindBestCephNorm(norms, "Convexity", "downs", patientAge: 8, patientSex: null);
        var forAdult = CephService.FindBestCephNorm(norms, "Convexity", "downs", patientAge: 45, patientSex: "M");
        var forUnknownAge = CephService.FindBestCephNorm(norms, "Convexity", "downs", patientAge: null, patientSex: null);

        forChild.Should().NotBeNull();
        forChild!.NormalValue.Should().Be(0m);
        forAdult.Should().NotBeNull();
        forAdult!.NormalValue.Should().Be(0m);
        forUnknownAge.Should().NotBeNull();
        forUnknownAge!.NormalValue.Should().Be(0m);

        // Mixed scenario: stratified adult row exists for SNA, but the patient
        // is a 5-year-old (below the child band 6–12). The un-stratified
        // fallback row should win because no stratified band matches.
        var mixed = new List<CephNorm>
        {
            Row("SNA", "steiner", 82, ageMin: 18, ageMax: null), // adult only
            Row("SNA", "steiner", 80),                           // un-stratified fallback
        };
        var toddler = CephService.FindBestCephNorm(mixed, "SNA", "steiner", patientAge: 5, patientSex: null);
        toddler.Should().NotBeNull();
        toddler!.NormalValue.Should().Be(80m, "the 5-year-old matches no age band, so the un-stratified fallback applies");
    }

    [Fact]
    public void CephNormLookup_PrefersSexSpecific_OverSexNull()
    {
        // Adult Wits: sex-null 0mm, sex-specific Male +1mm. A male patient must
        // get the sex-specific +1mm row, not the sex-null 0mm row.
        var norms = new List<CephNorm>
        {
            Row("Wits", "wits", 0m, ageMin: 18, ageMax: null, sex: null), // applies to both
            Row("Wits", "wits", 1m, ageMin: 18, ageMax: null, sex: "M"),  // male-specific
            Row("Wits", "wits", 0m, ageMin: 18, ageMax: null, sex: "F"),  // female-specific
        };

        // Male patient → sex-specific M (+1mm).
        var male = CephService.FindBestCephNorm(norms, "Wits", "wits", patientAge: 30, patientSex: "M");
        male.Should().NotBeNull();
        male!.NormalValue.Should().Be(1m, "the male-specific row must win over the sex-null row");
        male.Sex.Should().Be("M");

        // Female patient → sex-specific F (0mm).
        var female = CephService.FindBestCephNorm(norms, "Wits", "wits", patientAge: 30, patientSex: "F");
        female.Should().NotBeNull();
        female!.NormalValue.Should().Be(0m);
        female.Sex.Should().Be("F");

        // Unknown-sex patient → sex-null (0mm). When the patient's sex is
        // unknown we cannot pick a sex-specific row; the sex-null row wins.
        var unknownSex = CephService.FindBestCephNorm(norms, "Wits", "wits", patientAge: 30, patientSex: null);
        unknownSex.Should().NotBeNull();
        unknownSex!.Sex.Should().BeNull("when patient sex is unknown, the sex-null (both) row wins");
    }

    [Fact]
    public void CephNormLookup_AdultPatient_DoesNotMatchChildNorm()
    {
        // SNA: child 6-12 = 81°, adult 18+ = 82°.
        var norms = new List<CephNorm>
        {
            Row("SNA", "steiner", 81, ageMin: 6,  ageMax: 12),
            Row("SNA", "steiner", 82, ageMin: 18, ageMax: null),
        };

        // A 30-year-old must get the ADULT norm (82°), NEVER the child (81°).
        var best = CephService.FindBestCephNorm(norms, "SNA", "steiner", patientAge: 30, patientSex: null);

        best.Should().NotBeNull();
        best!.NormalValue.Should().Be(82m, "a 30-year-old is well above the 6–12 child band");
        best.AgeMin.Should().Be(18);
        best.AgeMax.Should().BeNull();

        // Boundary checks: 17-year-old matches adolescent (when present) or
        // nothing (when not). 18-year-old matches adult.
        var ageBoundaries = new List<CephNorm>
        {
            Row("SNA", "steiner", 81, ageMin: 6,  ageMax: 12),
            Row("SNA", "steiner", 81, ageMin: 13, ageMax: 17),
            Row("SNA", "steiner", 82, ageMin: 18, ageMax: null),
        };
        // 12-year-old: in the 6–12 band (inclusive upper bound).
        var at12 = CephService.FindBestCephNorm(ageBoundaries, "SNA", "steiner", patientAge: 12, patientSex: null);
        at12!.AgeMax.Should().Be(12, "12 is included in the 6–12 child band");
        // 13-year-old: in the 13–17 adolescent band (inclusive lower bound).
        var at13 = CephService.FindBestCephNorm(ageBoundaries, "SNA", "steiner", patientAge: 13, patientSex: null);
        at13!.AgeMin.Should().Be(13);
        // 17-year-old: still adolescent (inclusive upper bound).
        var at17 = CephService.FindBestCephNorm(ageBoundaries, "SNA", "steiner", patientAge: 17, patientSex: null);
        at17!.AgeMax.Should().Be(17);
        // 18-year-old: adult (inclusive lower bound).
        var at18 = CephService.FindBestCephNorm(ageBoundaries, "SNA", "steiner", patientAge: 18, patientSex: null);
        at18!.AgeMin.Should().Be(18);
        at18.AgeMax.Should().BeNull();

        // PATIENT-SAFETY: a 30-year-old must never match the 6–12 child row,
        // even if it's the only stratified row present (no adult row at all).
        var childOnly = new List<CephNorm>
        {
            Row("SNA", "steiner", 81, ageMin: 6, ageMax: 12),
        };
        var adultVsChildOnly = CephService.FindBestCephNorm(childOnly, "SNA", "steiner", patientAge: 30, patientSex: null);
        adultVsChildOnly.Should().BeNull(
            "a 30-year-old cannot match a 6–12 child row, and no un-stratified fallback exists — " +
            "the caller must use the built-in hardcoded default rather than a wrong-age norm");
    }
}

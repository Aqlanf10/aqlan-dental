using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephLateralGeometryEngineTests
{
    [Fact]
    public void Calculate_UsesFrozenVersionAndProducesDeterministicCoreAngles()
    {
        var landmarks = new Dictionary<string, CephGeometryPoint>
        {
            ["S"] = new(0, 0),
            ["N"] = new(10, 0),
            ["A"] = new(10, 10),
            ["B"] = new(0, 10),
        };

        var first = CephLateralGeometryEngine.Calculate(landmarks, 0, ["steiner"]);
        var second = CephLateralGeometryEngine.Calculate(landmarks, 0, ["steiner"]);

        CephLateralGeometryEngine.Version.Should().Be("ADP-CEPH-GEOMETRY-v1");
        first.Should().BeEquivalentTo(second, options => options.WithStrictOrdering());
        first.Single(item => item.Name == "SNA").Value.Should().Be(90);
        first.Single(item => item.Name == "SNB").Value.Should().Be(45);
        first.Single(item => item.Name == "ANB").Value.Should().Be(45);
    }

    [Fact]
    public void Calculate_RequiresCalibrationForLinearMeasurements()
    {
        var landmarks = new Dictionary<string, CephGeometryPoint>
        {
            ["Co"] = new(0, 0),
            ["A"] = new(30, 40),
            ["Gn"] = new(60, 80),
            ["ANS"] = new(10, 10),
            ["Me"] = new(10, 30),
        };

        var uncalibrated = CephLateralGeometryEngine.Calculate(landmarks, 0, ["mcnamara"]);
        var calibrated = CephLateralGeometryEngine.Calculate(landmarks, 10, ["mcnamara"]);

        uncalibrated.Should().BeEmpty();
        calibrated.Single(item => item.Name == "Co-A").Value.Should().Be(5);
        calibrated.Single(item => item.Name == "Co-Gn").Value.Should().Be(10);
        calibrated.Single(item => item.Name == "ANS-Me").Value.Should().Be(2);
    }

    [Fact]
    public void Calculate_WitsPrefersFunctionalMolarPlane()
    {
        var landmarks = new Dictionary<string, CephGeometryPoint>
        {
            ["U1T"] = new(0, 0),
            ["L1T"] = new(0, 2),
            ["U6"] = new(10, 0),
            ["L6"] = new(10, 2),
            ["Go"] = new(0, 50),
            ["Me"] = new(10, 60),
            ["A"] = new(8, 20),
            ["B"] = new(2, 30),
        };

        var result = CephLateralGeometryEngine.Calculate(landmarks, 2, ["wits"]);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Wits");
        result[0].Value.Should().Be(3);
        result[0].Unit.Should().Be("mm");
    }

    [Fact]
    public void Calculate_SoftTissuePlanesPreferSoftPogonion()
    {
        var landmarks = new Dictionary<string, CephGeometryPoint>
        {
            ["Pn"] = new(0, 0),
            ["Cm"] = new(0, 2),
            ["LS"] = new(2, 1),
            ["LI"] = new(3, 1),
            ["Pog"] = new(0, 20),
            ["SPog"] = new(1, 20),
        };

        var withSoftPogonion = CephLateralGeometryEngine.Calculate(landmarks, 1, ["steiner", "ricketts"]);
        landmarks.Remove("SPog");
        var legacyFallback = CephLateralGeometryEngine.Calculate(landmarks, 1, ["steiner", "ricketts"]);

        withSoftPogonion.Single(item => item.Name == "Upper-Lip-ELine").Value
            .Should().NotBe(legacyFallback.Single(item => item.Name == "Upper-Lip-ELine").Value);
        withSoftPogonion.Should().Contain(item => item.Name == "UL-SLine");
        withSoftPogonion.Should().Contain(item => item.Name == "LL-SLine");
    }

    [Fact]
    public void GeometryPrimitivesRejectDegenerateSegmentsWithoutNaN()
    {
        var point = new CephGeometryPoint(1, 1);

        CephLateralGeometryEngine.Angle(point, point, point).Should().Be(0);
        CephLateralGeometryEngine.AngleBetweenLines(point, point, point, point).Should().Be(0);
        CephLateralGeometryEngine.SignedPerpendicular(point, point, point).Should().Be(0);
    }
}

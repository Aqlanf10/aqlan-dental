using System.Reflection;
using AqlanDentalPro.API.Services;
using FluentAssertions;
using QRCoder;
using Xunit;

namespace AqlanDentalPro.UnitTests.Lab;

/// <summary>
/// LABINV-REQ-008 — the tracking code printed on the lab order slip.
///
/// <para>
/// These tests exist because the previous "barcode" was not one. Its bar widths came from
/// <c>value.Aggregate(17, (c, ch) =&gt; c * 31 + ch)</c> — a string hash — so it rendered a
/// convincing strip of bars under the words "LAB TRACKING" that no reader could decode.
/// A test asserting "bars were drawn" would have passed against that code, which is
/// exactly the kind of test worth not writing.
/// </para>
///
/// <para>
/// What is asserted instead is decodability: the emitted image must be a real QR whose
/// module matrix reproduces the order number when read back. The matrix here is produced
/// by QRCoder's own encoder, so these tests pin that the generator is <b>wired to a real
/// encoder at all</b> and that the payload is the order number — the property the old
/// code failed — rather than re-testing QRCoder's internals.
/// </para>
/// </summary>
public class LabOrderTrackingCodeTests
{
    /// <summary>
    /// Invokes the private renderer the PDF actually calls. Reflection is deliberate:
    /// testing a copy of the logic would not prove the slip carries a real code.
    /// </summary>
    private static byte[]? RenderQr(string value)
    {
        var method = typeof(LabOrderPdfGenerator)
            .GetMethod("TryRenderQrPng", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("the PDF generator must still render its tracking code through this method");

        return (byte[]?)method!.Invoke(null, [value]);
    }

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Produces_A_Real_Png()
    {
        var png = RenderQr("LAB-2026-003");

        png.Should().NotBeNull();
        png!.Length.Should().BeGreaterThan(PngMagic.Length);
        png.Take(PngMagic.Length).Should().Equal(PngMagic, "the slip embeds a PNG, not an arbitrary byte blob");
    }

    /// <summary>
    /// The assertion the old implementation could never have satisfied: the printed code
    /// must actually carry the order number.
    /// </summary>
    [Theory]
    [InlineData("LAB-2026-003")]
    [InlineData("LAB-2026-1042")]
    [InlineData("A")]
    public void Encodes_The_Order_Number_So_A_Reader_Recovers_It(string orderNumber)
    {
        // Independently encode the same payload and compare module matrices. If the
        // generator were hashing the string — or encoding anything other than the order
        // number — the matrices would not match.
        using var generator = new QRCodeGenerator();
        using var expected = generator.CreateQrCode(orderNumber, QRCodeGenerator.ECCLevel.Q);
        var expectedPng = new PngByteQRCode(expected).GetGraphic(8);

        var actual = RenderQr(orderNumber);

        actual.Should().NotBeNull();
        actual!.Should().Equal(expectedPng);
    }

    /// <summary>
    /// Two different orders must not print the same label. Obvious — and yet the hash-based
    /// predecessor could collide, because it reduced the whole number to one 32-bit int
    /// before drawing 36 bars from 24 of its bits.
    /// </summary>
    [Fact]
    public void Different_Orders_Produce_Different_Codes()
    {
        var a = RenderQr("LAB-2026-003");
        var b = RenderQr("LAB-2026-004");

        a.Should().NotBeNull();
        b.Should().NotBeNull();
        a!.Should().NotEqual(b!);
    }

    [Fact]
    public void Same_Order_Produces_A_Stable_Code_Across_Reprints()
    {
        RenderQr("LAB-2026-003").Should().Equal(RenderQr("LAB-2026-003"));
    }

    /// <summary>
    /// A code that cannot be produced must not take the work order down with it. The slip
    /// is what the lab physically works from; printing it without a QR is recoverable,
    /// not printing it at all stops the case.
    /// </summary>
    [Fact]
    public void An_Unrenderable_Value_Yields_No_Code_Rather_Than_Throwing()
    {
        var act = () => RenderQr(new string('X', 5000));

        act.Should().NotThrow();
    }
}

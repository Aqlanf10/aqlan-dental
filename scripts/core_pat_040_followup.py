from pathlib import Path

controller = Path("backend/src/AqlanDentalPro.API/Controllers/OrthoCasesController.cs")
text = controller.read_text(encoding="utf-8")
old = '''        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
'''
new = '''        try
        {
            var result = await service.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) when (ex.Message == ActivePatientWriteGuard.ErrorMessage)
        {
            return BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage });
        }
'''
if old not in text:
    raise SystemExit("Ortho create result block not found")
controller.write_text(text.replace(old, new, 1), encoding="utf-8")

contract = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientDependentWriteGuardContractTests.cs")
text = contract.read_text(encoding="utf-8")
old = '''    [Theory]
    [InlineData("backend/src/AqlanDentalPro.Infrastructure/Services/PaymentService.cs")]
'''
new = '''    [Fact]
    public void OrthoController_MapsTheServiceGuardToAnArabicBadRequest()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "backend/src/AqlanDentalPro.API/Controllers/OrthoCasesController.cs"));

        source.Should().Contain("catch (ArgumentException ex) when (ex.Message == ActivePatientWriteGuard.ErrorMessage)");
        source.Should().Contain("BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage })");
    }

    [Theory]
    [InlineData("backend/src/AqlanDentalPro.Infrastructure/Services/PaymentService.cs")]
'''
if old not in text:
    raise SystemExit("contract insertion target not found")
contract.write_text(text.replace(old, new, 1), encoding="utf-8")

Path("scripts/core_pat_040_followup.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-040-followup.yml").unlink(missing_ok=True)

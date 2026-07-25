from pathlib import Path

path = Path("backend/src/AqlanDentalPro.Application/Services/PatientService.cs")
text = path.read_text(encoding="utf-8")
old = '''            var numberPrefix = await patientSettings.GetNumberPrefixAsync();
        var number = await repo.GeneratePatientNumberAsync(numberPrefix);
'''
new = '''            var numberPrefix = await patientSettings.GetNumberPrefixAsync();
            var number = await repo.GeneratePatientNumberAsync(numberPrefix);
'''
if old not in text:
    raise SystemExit("PatientService indentation target not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
Path("scripts/core_pat_043_format.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-043-format.yml").unlink(missing_ok=True)

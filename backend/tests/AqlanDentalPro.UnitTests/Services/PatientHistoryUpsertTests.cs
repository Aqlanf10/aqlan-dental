using Xunit;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Unit tests for PatientService UpsertMedicalHistoryAsync and UpsertDentalHistoryAsync.
/// Verifies: create-when-missing, update-when-existing, invalid-patientId, and AddChildAsync usage.
/// </summary>
public class PatientHistoryUpsertTests
{
    private readonly Mock<IPatientRepository> _repoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IPatientSettingsReader> _patientSettingsMock;
    private readonly Mock<IPatientPortalService> _portalServiceMock;
    private readonly Mock<IClinicClock> _clinicClockMock;
    private readonly Mock<ILogger<PatientService>> _loggerMock;
    private readonly PatientService _service;

    public PatientHistoryUpsertTests()
    {
        _repoMock = new Mock<IPatientRepository>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _patientSettingsMock = new Mock<IPatientSettingsReader>();
        _portalServiceMock = new Mock<IPatientPortalService>();
        _clinicClockMock = new Mock<IClinicClock>();
        _loggerMock = new Mock<ILogger<PatientService>>();

        _currentUserMock.Setup(c => c.BranchId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(c => c.IsAdmin).Returns(false);
        _patientSettingsMock.Setup(s => s.GetNumberPrefixAsync(It.IsAny<CancellationToken>())).ReturnsAsync("GM");
        _clinicClockMock.Setup(c => c.Today()).Returns(new DateOnly(2026, 1, 1));
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);

        _service = new PatientService(_repoMock.Object, _currentUserMock.Object, _patientSettingsMock.Object, _portalServiceMock.Object, _clinicClockMock.Object, _loggerMock.Object);
    }

    private static Patient CreateTestPatient(Guid? id = null)
    {
        return new Patient
        {
            Id = id ?? Guid.NewGuid(),
            PatientNumber = "GM-2026-001",
            FirstName = "أحمد",
            LastName = "محمد",
            Phone = "0501234567",
            IsActive = true,
            MedicalHistory = null,
            DentalHistory = null
        };
    }

    // =====================================================================
    // 1. Patient with no medical history: PUT creates successfully
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_WhenNoExistingHistory_CreatesNewHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto
        {
            ChronicDiseases = "Diabetes",
            CurrentMedications = "Metformin",
            DrugAllergies = "Penicillin",
            BleedingDisorders = true,
            IsPregnant = "no",
            TmjProblems = false,
            PreviousSurgeries = "Appendectomy",
            Notes = "Test notes"
        };

        // Act
        var result = await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChronicDiseases.Should().Be("Diabetes");
        result.CurrentMedications.Should().Be("Metformin");
        result.DrugAllergies.Should().Be("Penicillin");
        result.BleedingDisorders.Should().BeTrue();
        result.IsPregnant.Should().Be("no");
        result.TmjProblems.Should().BeFalse();
        result.PreviousSurgeries.Should().Be("Appendectomy");
        result.Notes.Should().Be("Test notes");

        // Verify AddChildAsync was called for the new entity
        _repoMock.Verify(r => r.AddChildAsync<MedicalHistory>(It.IsAny<MedicalHistory>()), Times.Once);
        // Verify Update was NOT called
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
        // Verify SaveChanges was called
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // =====================================================================
    // 2. Patient with existing medical history: PUT updates successfully
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_WhenExistingHistory_UpdatesHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.MedicalHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChronicDiseases = "Old disease",
            CurrentMedications = "Old med",
            IsActive = true
        };
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto
        {
            ChronicDiseases = "Updated disease",
            CurrentMedications = "Updated med",
            DrugAllergies = null,
            BleedingDisorders = false,
            IsPregnant = "na",
            TmjProblems = true,
            PreviousSurgeries = null,
            Notes = "Updated notes"
        };

        // Act
        var result = await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChronicDiseases.Should().Be("Updated disease");
        result.CurrentMedications.Should().Be("Updated med");
        result.TmjProblems.Should().BeTrue();

        // Verify AddChildAsync was NOT called (we are updating, not creating)
        _repoMock.Verify(r => r.AddChildAsync<MedicalHistory>(It.IsAny<MedicalHistory>()), Times.Never);
        // Verify Update was NOT called on patient (change-tracker handles it)
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
        // Verify SaveChanges was called
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // =====================================================================
    // 3. Patient with no dental history: PUT creates successfully
    // =====================================================================

    [Fact]
    public async Task UpsertDentalHistoryAsync_WhenNoExistingHistory_CreatesNewHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new DentalHistoryDto
        {
            ChiefComplaint = "Tooth pain",
            PreviousTreatments = "Root canal",
            MouthBreathing = true,
            Bruxism = false,
            ThumbSucking = false,
            TongueThrusing = true,
            Notes = "Dental notes"
        };

        // Act
        var result = await _service.UpsertDentalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChiefComplaint.Should().Be("Tooth pain");
        result.PreviousTreatments.Should().Be("Root canal");
        result.MouthBreathing.Should().BeTrue();
        result.Bruxism.Should().BeFalse();
        result.ThumbSucking.Should().BeFalse();
        result.TongueThrusing.Should().BeTrue();
        result.Notes.Should().Be("Dental notes");

        // Verify AddChildAsync was called for the new entity
        _repoMock.Verify(r => r.AddChildAsync<DentalHistory>(It.IsAny<DentalHistory>()), Times.Once);
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // =====================================================================
    // 4. Patient with existing dental history: PUT updates successfully
    // =====================================================================

    [Fact]
    public async Task UpsertDentalHistoryAsync_WhenExistingHistory_UpdatesHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.DentalHistory = new DentalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChiefComplaint = "Old complaint",
            PreviousTreatments = "Old treatment",
            IsActive = true
        };
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new DentalHistoryDto
        {
            ChiefComplaint = "New complaint",
            PreviousTreatments = "New treatment",
            MouthBreathing = false,
            Bruxism = true,
            ThumbSucking = true,
            TongueThrusing = false,
            Notes = "Updated dental notes"
        };

        // Act
        var result = await _service.UpsertDentalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChiefComplaint.Should().Be("New complaint");
        result.PreviousTreatments.Should().Be("New treatment");
        result.Bruxism.Should().BeTrue();
        result.ThumbSucking.Should().BeTrue();

        // Verify AddChildAsync was NOT called (we are updating, not creating)
        _repoMock.Verify(r => r.AddChildAsync<DentalHistory>(It.IsAny<DentalHistory>()), Times.Never);
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // =====================================================================
    // 5. Invalid patientId: returns null (maps to 404 in controller)
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_InvalidPatientId_ReturnsNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(invalidId))
            .ReturnsAsync((Patient?)null);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Test" };

        // Act
        var result = await _service.UpsertMedicalHistoryAsync(invalidId, dto);

        // Assert
        result.Should().BeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertDentalHistoryAsync_InvalidPatientId_ReturnsNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(invalidId))
            .ReturnsAsync((Patient?)null);

        var dto = new DentalHistoryDto { ChiefComplaint = "Test" };

        // Act
        var result = await _service.UpsertDentalHistoryAsync(invalidId, dto);

        // Assert
        result.Should().BeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // =====================================================================
    // 6. Inactive (archived) patient: returns null (maps to 404)
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_ArchivedPatient_ReturnsNull()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.IsActive = false;
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Test" };

        // Act
        var result = await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().BeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // =====================================================================
    // 7. Soft-deleted medical history row is restored on upsert
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_SoftDeletedHistory_RestoresHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.MedicalHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChronicDiseases = "Old",
            IsActive = false,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = Guid.NewGuid()
        };
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Restored" };

        // Act
        var result = await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChronicDiseases.Should().Be("Restored");
        patient.MedicalHistory.IsActive.Should().BeTrue();
        patient.MedicalHistory.DeletedAt.Should().BeNull();
        patient.MedicalHistory.DeletedBy.Should().BeNull();
    }

    // =====================================================================
    // 8. Soft-deleted dental history row is restored on upsert
    // =====================================================================

    [Fact]
    public async Task UpsertDentalHistoryAsync_SoftDeletedHistory_RestoresHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.DentalHistory = new DentalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChiefComplaint = "Old",
            IsActive = false,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = Guid.NewGuid()
        };
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new DentalHistoryDto { ChiefComplaint = "Restored" };

        // Act
        var result = await _service.UpsertDentalHistoryAsync(patient.Id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.ChiefComplaint.Should().Be("Restored");
        patient.DentalHistory.IsActive.Should().BeTrue();
        patient.DentalHistory.DeletedAt.Should().BeNull();
        patient.DentalHistory.DeletedBy.Should().BeNull();
    }

    // =====================================================================
    // 9. New MedicalHistory row is linked to correct PatientId
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_NewHistory_SetsCorrectPatientId()
    {
        // Arrange
        var patient = CreateTestPatient();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Test" };

        MedicalHistory? capturedEntity = null;
        _repoMock.Setup(r => r.AddChildAsync<MedicalHistory>(It.IsAny<MedicalHistory>()))
            .Callback<MedicalHistory>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert
        capturedEntity.Should().NotBeNull();
        capturedEntity!.PatientId.Should().Be(patient.Id);
    }

    // =====================================================================
    // 10. New DentalHistory row is linked to correct PatientId
    // =====================================================================

    [Fact]
    public async Task UpsertDentalHistoryAsync_NewHistory_SetsCorrectPatientId()
    {
        // Arrange
        var patient = CreateTestPatient();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new DentalHistoryDto { ChiefComplaint = "Test" };

        DentalHistory? capturedEntity = null;
        _repoMock.Setup(r => r.AddChildAsync<DentalHistory>(It.IsAny<DentalHistory>()))
            .Callback<DentalHistory>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpsertDentalHistoryAsync(patient.Id, dto);

        // Assert
        capturedEntity.Should().NotBeNull();
        capturedEntity!.PatientId.Should().Be(patient.Id);
    }

    // =====================================================================
    // 11. GET returns null MedicalHistory safely in profile DTO
    // =====================================================================

    [Fact]
    public async Task GetByIdAsync_WhenNoMedicalHistory_ProfileDtoHasNullMedicalHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.MedicalHistory = null;
        _repoMock.Setup(r => r.GetWithHistoriesAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act
        var result = await _service.GetByIdAsync(patient.Id);

        // Assert
        result.Should().NotBeNull();
        result!.MedicalHistory.Should().BeNull();
    }

    // =====================================================================
    // 12. GET returns null DentalHistory safely in profile DTO
    // =====================================================================

    [Fact]
    public async Task GetByIdAsync_WhenNoDentalHistory_ProfileDtoHasNullDentalHistory()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.DentalHistory = null;
        _repoMock.Setup(r => r.GetWithHistoriesAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act
        var result = await _service.GetByIdAsync(patient.Id);

        // Assert
        result.Should().NotBeNull();
        result!.DentalHistory.Should().BeNull();
    }

    // =====================================================================
    // 13. New medical history does NOT use repo.Update(patient)
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_NewHistory_DoesNotCallUpdateOnPatient()
    {
        // Arrange
        var patient = CreateTestPatient();
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Test" };

        // Act
        await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert — repo.Update(patient) must never be called for child-entity creation
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
    }

    // =====================================================================
    // 14. Existing medical history update does NOT use repo.Update(patient)
    // =====================================================================

    [Fact]
    public async Task UpsertMedicalHistoryAsync_ExistingHistory_DoesNotCallUpdateOnPatient()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.MedicalHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChronicDiseases = "Old",
            IsActive = true
        };
        _repoMock.Setup(r => r.GetWithHistoriesIgnoreFiltersAsync(patient.Id))
            .ReturnsAsync(patient);
        _repoMock.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var dto = new MedicalHistoryDto { ChronicDiseases = "Updated" };

        // Act
        await _service.UpsertMedicalHistoryAsync(patient.Id, dto);

        // Assert — change tracker detects property mutations; no explicit Update call needed
        _repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
    }
}

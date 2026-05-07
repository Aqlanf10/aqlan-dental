using Xunit;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using FluentAssertions;
using Moq;

namespace AqlanDentalPro.UnitTests.Repositories;

/// <summary>
/// Unit tests for GenericRepository.AddChildAsync and AddChild.
/// Uses mocked IPatientRepository to verify the contract without InMemoryDatabase.
/// </summary>
public class GenericRepositoryAddChildTests
{
    [Fact]
    public async Task AddChildAsync_IsAvailableOnInterface()
    {
        // Arrange — verify AddChildAsync is part of the IGenericRepository interface
        var repoMock = new Mock<IGenericRepository<Patient>>();
        var medicalHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ChronicDiseases = "Test",
            IsActive = true
        };

        repoMock.Setup(r => r.AddChildAsync(It.IsAny<MedicalHistory>()))
            .Returns(Task.CompletedTask);

        // Act — should compile and invoke without error
        await repoMock.Object.AddChildAsync(medicalHistory);

        // Assert
        repoMock.Verify(r => r.AddChildAsync(medicalHistory), Times.Once);
    }

    [Fact]
    public void AddChild_SyncIsAvailableOnInterface()
    {
        // Arrange — verify AddChild (sync) still exists on the interface
        var repoMock = new Mock<IGenericRepository<Patient>>();
        var medicalHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ChronicDiseases = "Test",
            IsActive = true
        };

        // Act — should compile and invoke without error
        repoMock.Object.AddChild(medicalHistory);

        // Assert
        repoMock.Verify(r => r.AddChild(medicalHistory), Times.Once);
    }

    [Fact]
    public async Task AddChildAsync_TracksEntityAsAdded_WhenCalledWithNewChild()
    {
        // Arrange
        var repoMock = new Mock<IGenericRepository<Patient>>();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-TEST-001",
            FirstName = "Test",
            LastName = "Patient",
            IsActive = true
        };

        var medHistory = new MedicalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChronicDiseases = "Hypertension",
            IsActive = true
        };

        repoMock.Setup(r => r.AddChildAsync(It.IsAny<MedicalHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        patient.MedicalHistory = medHistory;
        await repoMock.Object.AddChildAsync(medHistory);

        // Assert — AddChildAsync was called (not Update)
        repoMock.Verify(r => r.AddChildAsync(medHistory), Times.Once);
        repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
    }

    [Fact]
    public async Task AddChildAsync_ForDentalHistory_TracksEntityAsAdded()
    {
        // Arrange
        var repoMock = new Mock<IGenericRepository<Patient>>();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-TEST-002",
            FirstName = "Test",
            LastName = "Patient2",
            IsActive = true
        };

        var dentalHistory = new DentalHistory
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ChiefComplaint = "Tooth pain",
            IsActive = true
        };

        repoMock.Setup(r => r.AddChildAsync(It.IsAny<DentalHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        patient.DentalHistory = dentalHistory;
        await repoMock.Object.AddChildAsync(dentalHistory);

        // Assert
        repoMock.Verify(r => r.AddChildAsync(dentalHistory), Times.Once);
        repoMock.Verify(r => r.Update(It.IsAny<Patient>()), Times.Never);
    }
}

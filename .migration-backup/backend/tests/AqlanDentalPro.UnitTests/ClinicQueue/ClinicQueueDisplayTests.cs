using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// Tests for the /api/clinic-queue/display endpoint safety.
/// Validates that the endpoint returns 200 with safe empty data
/// even when the database is empty or encounters errors.
/// </summary>
public class ClinicQueueDisplayTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ClinicQueueController CreateController(AppDbContext db)
    {
        var pushService = new Mock<IRealTimePushService>().Object;
        var smsService = new Mock<ISmsService>().Object;
        var whatsAppService = new Mock<IWhatsAppService>().Object;
        var auditService = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<ClinicQueueController>>().Object;

        return new ClinicQueueController(db, pushService, smsService, whatsAppService, auditService, logger);
    }

    [Fact]
    public async Task GetDisplay_EmptyQueue_Returns200WithSafeEmptyData()
    {
        // Arrange
        using var db = CreateDb();
        var controller = CreateController(db);

        // Act
        var result = await controller.GetDisplay();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        ok!.StatusCode.Should().Be(200);

        // The response should have the safe empty structure
        var response = ok.Value!;
        var responseType = response.GetType();

        // Check that the properties exist and have safe default values
        var latestCalledProp = responseType.GetProperty("LatestCalled");
        latestCalledProp.Should().NotBeNull();
        latestCalledProp!.GetValue(response).Should().BeNull();

        var waitingCountProp = responseType.GetProperty("WaitingCount");
        waitingCountProp.Should().NotBeNull();
        waitingCountProp!.GetValue(response).Should().Be(0);

        var avgTimeProp = responseType.GetProperty("AverageServiceTimeMinutes");
        avgTimeProp.Should().NotBeNull();
        Convert.ToDouble(avgTimeProp!.GetValue(response)).Should().Be(15.0);
    }

    [Fact]
    public async Task GetDisplay_WithWaitingPatient_ReturnsCorrectStructure()
    {
        // Arrange
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var patient = new Patient
        {
            Id = patientId,
            FirstName = "أحمد",
            LastName = "محمد",
            PatientNumber = "P-001",
            Phone = "0501234567",
            IsActive = true
        };

        var doctor = new Doctor
        {
            Id = doctorId,
            Name = "د. خالد",
            IsActive = true
        };

        db.Patients.Add(patient);
        db.Doctors.Add(doctor);

        var today = ClinicTimeProvider.ClinicToday();
        var queueItem = new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.Waiting,
            QueueDate = today,
            IsActive = true,
            RoomName = "غرفة 1"
        };
        db.ClinicQueueItems.Add(queueItem);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetDisplay();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        var response = ok!.Value!;
        var responseType = response.GetType();

        var waitingCountProp = responseType.GetProperty("WaitingCount");
        waitingCountProp.Should().NotBeNull();
        waitingCountProp!.GetValue(response).Should().Be(1);
    }

    [Fact]
    public async Task GetDisplay_PublicWaitingList_DoesNotExposeFullPatientName()
    {
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Ali",
            MiddleName = "Aqlan",
            LastName = "Alkamel",
            PatientNumber = "P-PRIVACY",
            IsActive = true
        });
        db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr Test", IsActive = true });
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.Waiting,
            QueueDate = ClinicTimeProvider.ClinicToday(),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetDisplay();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var waitingList = ok.Value!.GetType().GetProperty("WaitingList")!.GetValue(ok.Value) as System.Collections.IEnumerable;
        var first = waitingList!.Cast<object>().Single();
        var patientName = first.GetType().GetProperty("PatientName")!.GetValue(first)?.ToString();

        patientName.Should().Be("Ali A.");
        patientName.Should().NotContain("Aqlan");
        patientName.Should().NotContain("Alkamel");
    }

    [Fact]
    public async Task GetDisplay_CalledPatient_AppearsInLatestCalled()
    {
        // Arrange
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "سارة",
            LastName = "علي",
            PatientNumber = "P-002",
            IsActive = true
        });
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. فهد",
            IsActive = true
        });

        var today = ClinicTimeProvider.ClinicToday();
        var queueItem = new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.Called,
            QueueDate = today,
            IsActive = true,
            CalledAt = DateTime.UtcNow,
            RoomName = "غرفة 2"
        };
        db.ClinicQueueItems.Add(queueItem);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetDisplay();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        var response = ok!.Value!;
        var responseType = response.GetType();

        // LatestCalled should be populated
        var latestCalledProp = responseType.GetProperty("LatestCalled");
        latestCalledProp.Should().NotBeNull();
        var latestCalled = latestCalledProp!.GetValue(response);
        latestCalled.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDisplay_PublicLatestCalled_DoesNotExposeFullPatientName()
    {
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Sara",
            MiddleName = "Aqlan",
            LastName = "Alkamel",
            PatientNumber = "P-CALL",
            IsActive = true
        });
        db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr Test", IsActive = true });
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.Called,
            QueueDate = ClinicTimeProvider.ClinicToday(),
            IsActive = true,
            CalledAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetDisplay();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var latestCalled = ok.Value!.GetType().GetProperty("LatestCalled")!.GetValue(ok.Value);
        var patientName = latestCalled!.GetType().GetProperty("PatientName")!.GetValue(latestCalled)?.ToString();

        patientName.Should().Be("Sara A.");
        patientName.Should().NotContain("Aqlan");
        patientName.Should().NotContain("Alkamel");
    }

    [Fact]
    public async Task GetDisplay_PublicLatestCalled_WithCompoundFirstName_OnlyShowsFirstToken()
    {
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "TEST QA PATIENT 730",
            LastName = "DoNotUseRealData",
            PatientNumber = "P-COMPOUND",
            IsActive = true
        });
        db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr Test", IsActive = true });
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.Called,
            QueueDate = ClinicTimeProvider.ClinicToday(),
            IsActive = true,
            CalledAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetDisplay();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var latestCalled = ok.Value!.GetType().GetProperty("LatestCalled")!.GetValue(ok.Value);
        var patientName = latestCalled!.GetType().GetProperty("PatientName")!.GetValue(latestCalled)?.ToString();

        patientName.Should().Be("TEST D.");
        patientName.Should().NotContain("QA");
        patientName.Should().NotContain("PATIENT");
        patientName.Should().NotContain("730");
        patientName.Should().NotContain("DoNotUseRealData");
    }

    [Fact]
    public async Task GetDisplay_PublicRecentlyCalled_WithCompoundFirstName_DoesNotExposeFullPatientName()
    {
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "TEST QA PATIENT 347",
            LastName = "DoNotUseRealData",
            PatientNumber = "P-RECENT",
            IsActive = true
        });
        db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr Test", IsActive = true });
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.InRoom,
            QueueDate = ClinicTimeProvider.ClinicToday(),
            IsActive = true,
            CalledAt = DateTime.UtcNow,
            RoomName = "Room 1"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetDisplay();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var recentlyCalled = ok.Value!.GetType().GetProperty("RecentlyCalled")!.GetValue(ok.Value) as System.Collections.IEnumerable;
        var first = recentlyCalled!.Cast<object>().Single();
        var patientName = first.GetType().GetProperty("PatientName")!.GetValue(first)?.ToString();

        patientName.Should().Be("TEST D.");
        patientName.Should().NotContain("QA");
        patientName.Should().NotContain("PATIENT");
        patientName.Should().NotContain("347");
        patientName.Should().NotContain("DoNotUseRealData");
    }

    [Fact]
    public async Task GetDisplay_InProgressPatient_WithNullDoctor_DoesNotCrash()
    {
        // Arrange
        using var db = CreateDb();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريم",
            LastName = "حسن",
            PatientNumber = "P-003",
            IsActive = true
        });

        var today = ClinicTimeProvider.ClinicToday();
        // InProgress item with a RoomName but no Doctor loaded (DoctorId is set but Doctor navigation null)
        var queueItem = new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = Guid.NewGuid(), // Doctor exists in DB but not loaded via Include in some scenarios
            Status = ClinicQueueStatus.InProgress,
            QueueDate = today,
            IsActive = true,
            RoomName = "غرفة 3",
            StartedAt = DateTime.UtcNow
        };
        db.ClinicQueueItems.Add(queueItem);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act — should not throw
        var result = await controller.GetDisplay();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetDisplay_InProgressWithDoctorAndPatient_AppearsInNowServing()
    {
        // Arrange
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "نورة",
            LastName = "سعد",
            PatientNumber = "P-004",
            IsActive = true
        });
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. عبدالله",
            IsActive = true
        });

        var today = ClinicTimeProvider.ClinicToday();
        var queueItem = new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.InProgress,
            QueueDate = today,
            IsActive = true,
            RoomName = "غرفة 1",
            StartedAt = DateTime.UtcNow
        };
        db.ClinicQueueItems.Add(queueItem);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetDisplay();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        var response = ok!.Value!;
        var responseType = response.GetType();

        // NowServing should have entries
        var nowServingProp = responseType.GetProperty("NowServing");
        nowServingProp.Should().NotBeNull();
        var nowServing = nowServingProp!.GetValue(response) as System.Collections.IList;
        nowServing.Should().NotBeNull();
        nowServing!.Count.Should().Be(1);
    }
}

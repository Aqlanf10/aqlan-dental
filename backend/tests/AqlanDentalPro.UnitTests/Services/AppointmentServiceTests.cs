using Xunit;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Unit tests for AppointmentService.
/// Uses Moq to mock IAppointmentRepository, ICurrentUserService,
/// IServiceScopeFactory, and ILogger.
/// Tests cover: GetDailyStatsAsync logic, CheckConflictAsync validation,
/// and UpdateStatusAsync status-transition validation.
/// </summary>
public class AppointmentServiceTests
{
    private readonly Mock<IAppointmentRepository> _repoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<ILogger<AppointmentService>> _loggerMock = new();

    private AppointmentService CreateService()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(c => c.BranchId).Returns((Guid?)null);
        return new AppointmentService(
            _repoMock.Object,
            _currentUserMock.Object,
            _scopeFactoryMock.Object,
            _loggerMock.Object);
    }

    // ─── GetDailyStatsAsync Tests ───────────────────────────────────────────

    [Fact]
    public async Task GetDailyStatsAsync_EmptyList_ReturnsZeroStats()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>()))
            .ReturnsAsync(Array.Empty<Appointment>());

        var service = CreateService();
        var date = new DateOnly(2025, 1, 15);

        // Act
        var stats = await service.GetDailyStatsAsync(date);

        // Assert
        stats.Total.Should().Be(0);
        stats.Completed.Should().Be(0);
        stats.Cancelled.Should().Be(0);
        stats.NoShow.Should().Be(0);
        stats.InProgress.Should().Be(0);
        stats.Waiting.Should().Be(0);
        stats.CompletionRate.Should().Be(0);
        stats.NoShowRate.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyStatsAsync_MixedStatuses_CalculatesCorrectly()
    {
        // Arrange
        var appointments = new List<Appointment>
        {
            new() { Status = AppointmentStatus.Completed },
            new() { Status = AppointmentStatus.Completed },
            new() { Status = AppointmentStatus.Cancelled },
            new() { Status = AppointmentStatus.NoShow },
            new() { Status = AppointmentStatus.InProgress },
            new() { Status = AppointmentStatus.Called },
            new() { Status = AppointmentStatus.Waiting },
            new() { Status = AppointmentStatus.Scheduled },
        };

        _repoMock
            .Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>()))
            .ReturnsAsync(appointments);

        var service = CreateService();

        // Act
        var stats = await service.GetDailyStatsAsync(new DateOnly(2025, 1, 15));

        // Assert
        stats.Total.Should().Be(8);
        stats.Completed.Should().Be(2);
        stats.Cancelled.Should().Be(1);
        stats.NoShow.Should().Be(1);
        // InProgress includes Called and InRoom statuses
        stats.InProgress.Should().Be(2); // InProgress + Called
        // Waiting includes Arrived, Scheduled, Confirmed statuses
        stats.Waiting.Should().Be(2); // Waiting + Scheduled
        stats.CompletionRate.Should().Be(25.0);  // 2/8 * 100
        stats.NoShowRate.Should().Be(12.5);      // 1/8 * 100
    }

    [Fact]
    public async Task GetDailyStatsAsync_AllCompleted_HasFullCompletionRate()
    {
        // Arrange
        var appointments = new List<Appointment>
        {
            new() { Status = AppointmentStatus.Completed },
            new() { Status = AppointmentStatus.Completed },
            new() { Status = AppointmentStatus.Completed },
        };

        _repoMock
            .Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>()))
            .ReturnsAsync(appointments);

        var service = CreateService();

        // Act
        var stats = await service.GetDailyStatsAsync(new DateOnly(2025, 1, 15));

        // Assert
        stats.Total.Should().Be(3);
        stats.Completed.Should().Be(3);
        stats.CompletionRate.Should().Be(100.0);
        stats.NoShowRate.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyStatsAsync_InRoom_StatusCountedAsInProgress()
    {
        // Arrange
        var appointments = new List<Appointment>
        {
            new() { Status = AppointmentStatus.InRoom },
        };

        _repoMock
            .Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>()))
            .ReturnsAsync(appointments);

        var service = CreateService();

        // Act
        var stats = await service.GetDailyStatsAsync(new DateOnly(2025, 1, 15));

        // Assert
        stats.InProgress.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyStatsAsync_Arrived_StatusCountedAsWaiting()
    {
        // Arrange
        var appointments = new List<Appointment>
        {
            new() { Status = AppointmentStatus.Arrived },
            new() { Status = AppointmentStatus.Confirmed },
        };

        _repoMock
            .Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>()))
            .ReturnsAsync(appointments);

        var service = CreateService();

        // Act
        var stats = await service.GetDailyStatsAsync(new DateOnly(2025, 1, 15));

        // Assert
        stats.Waiting.Should().Be(2);
    }

    // ─── CheckConflictAsync Validation Tests ────────────────────────────────

    [Fact]
    public async Task CheckConflictAsync_InvalidDate_ThrowsArgumentException()
    {
        var service = CreateService();

        var act = () => service.CheckConflictAsync(
            Guid.NewGuid(), "not-a-date", "10:00", 30, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid date format");
    }

    [Fact]
    public async Task CheckConflictAsync_InvalidTime_ThrowsArgumentException()
    {
        var service = CreateService();

        var act = () => service.CheckConflictAsync(
            Guid.NewGuid(), "2025-01-15", "not-a-time", 30, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid time format");
    }

    [Fact]
    public async Task CheckConflictAsync_ValidInput_CallsRepositoryWithCorrectEndTime()
    {
        // Arrange
        _repoMock
            .Setup(r => r.HasConflictAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(false);

        var service = CreateService();
        var doctorId = Guid.NewGuid();
        var date = "2025-01-15";
        var startTime = "10:00";
        var durationMinutes = 30;

        // Act
        var result = await service.CheckConflictAsync(doctorId, date, startTime, durationMinutes, null);

        // Assert
        result.Should().BeFalse();
        _repoMock.Verify(r => r.HasConflictAsync(
            doctorId,
            new DateOnly(2025, 1, 15),
            new TimeOnly(10, 0),
            new TimeOnly(10, 30),
            null), Times.Once);
    }

    [Fact]
    public async Task CheckConflictAsync_WithExcludeId_PassesItToRepository()
    {
        // Arrange
        var excludeId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.HasConflictAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(),
                excludeId))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.CheckConflictAsync(
            Guid.NewGuid(), "2025-01-15", "14:00", 60, excludeId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConflictAsync_DurationCalculatesEndTimeCorrectly()
    {
        // Arrange
        _repoMock
            .Setup(r => r.HasConflictAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act: 09:30 + 45 min = 10:15
        await service.CheckConflictAsync(Guid.NewGuid(), "2025-03-01", "09:30", 45, null);

        // Assert
        _repoMock.Verify(r => r.HasConflictAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateOnly>(),
            new TimeOnly(9, 30),
            new TimeOnly(10, 15),
            It.IsAny<Guid?>()), Times.Once);
    }

    // ─── UpdateStatusAsync Tests ───────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_AppointmentNotFound_ReturnsError()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Appointment?)null);

        var service = CreateService();

        // Act
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Confirmed");

        // Assert
        error.Should().Be("الموعد غير موجود");
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidStatusString_ReturnsError()
    {
        // Arrange
        var appointment = new Appointment { Status = AppointmentStatus.Scheduled };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "NotARealStatus");

        // Assert
        error.Should().Be("حالة الموعد غير صالحة");
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ScheduledToConfirmed_IsAllowed()
    {
        // Arrange
        var appointment = new Appointment { Status = AppointmentStatus.Scheduled };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Confirmed");

        // Assert
        error.Should().BeNull();
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        _repoMock.Verify(r => r.Update(appointment), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ScheduledToInProgress_IsBlockedByTransitionRules()
    {
        // Arrange
        var appointment = new Appointment { Status = AppointmentStatus.Scheduled };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "InProgress");

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("لا يمكن تغيير حالة الموعد");
    }

    [Fact]
    public async Task UpdateStatusAsync_CompletedToScheduled_IsBlocked()
    {
        // Arrange
        var appointment = new Appointment { Status = AppointmentStatus.Completed };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Scheduled");

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("لا يمكن تغيير حالة الموعد");
    }

    [Fact]
    public async Task UpdateStatusAsync_IdempotentTransition_IsAllowed()
    {
        // Arrange
        var appointment = new Appointment { Status = AppointmentStatus.Scheduled };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act: Same status → same status (idempotent)
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Scheduled");

        // Assert
        error.Should().BeNull();
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task UpdateStatusAsync_NoShowCanReschedule_SkipsTransitionValidation()
    {
        // Arrange — NoShow is a terminal state that allows re-scheduling
        var appointment = new Appointment { Status = AppointmentStatus.NoShow };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act: NoShow → Scheduled should be allowed (re-scheduling)
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Scheduled");

        // Assert
        error.Should().BeNull();
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task UpdateStatusAsync_CancelledCanReschedule_SkipsTransitionValidation()
    {
        // Arrange — Cancelled is a terminal state that allows re-scheduling
        var appointment = new Appointment { Status = AppointmentStatus.Cancelled };
        _repoMock
            .Setup(r => r.GetWithDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(appointment);

        var service = CreateService();

        // Act: Cancelled → Scheduled should be allowed (re-scheduling)
        var (result, error) = await service.UpdateStatusAsync(Guid.NewGuid(), "Scheduled");

        // Assert
        error.Should().BeNull();
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
    }
}

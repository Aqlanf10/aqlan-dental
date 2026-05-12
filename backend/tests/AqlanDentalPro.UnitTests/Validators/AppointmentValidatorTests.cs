using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Validators;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

public class AppointmentValidatorTests
{
    private readonly CreateAppointmentRequestValidator _validator = new();

    [Fact]
    public void ValidAppointment_ShouldPass()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingPatientId_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.Empty,
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PatientId");
    }

    [Fact]
    public void MissingDoctorId_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.Empty,
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DoctorId");
    }

    [Fact]
    public void AppointmentDate_InvalidFormat_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "15/06/2026",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AppointmentDate");
    }

    [Fact]
    public void AppointmentDate_Empty_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AppointmentDate");
    }

    [Fact]
    public void DurationMinutes_LessThan5_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 3,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DurationMinutes");
    }

    [Fact]
    public void DurationMinutes_GreaterThan480_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 500,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DurationMinutes");
    }

    [Fact]
    public void DurationMinutes_AtBoundary5_ShouldPass()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 5,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DurationMinutes_AtBoundary480_ShouldPass()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 480,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void StartTime_InvalidFormat_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "not-a-time",
            DurationMinutes = 30,
            AppointmentType = "فحص أولي"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StartTime");
    }

    [Fact]
    public void AppointmentType_TooLong_ShouldFail()
    {
        var req = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = "2026-06-15",
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = new string('A', 101)
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AppointmentType");
    }
}

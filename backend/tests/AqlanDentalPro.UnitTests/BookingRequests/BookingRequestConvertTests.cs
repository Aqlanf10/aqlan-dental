using AqlanDentalPro.Infrastructure.Repositories;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.BookingRequests;

/// <summary>
/// Tests for the booking request → appointment conversion workflow.
/// Covers:
///   1. Confirmed booking request can convert to appointment.
///   2. Current booking request does not conflict with itself during conversion.
///   3. Existing real appointment at same doctor/date/time still blocks conversion.
///   4. Already converted booking request cannot convert again.
/// Also tests the ConvertBookingRequestToAppointmentDto validator allows Guid.Empty PatientId.
/// </summary>
public class BookingRequestConvertTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static BookingRequestService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<BookingRequestService>>();
        // CORE-PAT-010: conversion now routes through the real
        // PatientService.CreateAsync (patient number, normalized phones, portal
        // account) instead of hand-rolling a Patient row.
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.IsAdmin).Returns(true);
        var patientSettings = new Mock<IPatientSettingsReader>();
        patientSettings.Setup(s => s.GetNumberPrefixAsync(It.IsAny<CancellationToken>())).ReturnsAsync("GM");
        var portal = new Mock<IPatientPortalService>();
        portal.Setup(x => x.EnsurePatientAccountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
              .ReturnsAsync(("user", "pass"));
        var clinicClock = new Mock<IClinicClock>();
        clinicClock.Setup(c => c.Today()).Returns(new DateOnly(2026, 1, 1));
        var patientService = new PatientService(
            new PatientRepository(db), currentUser.Object, patientSettings.Object,
            portal.Object, clinicClock.Object, new Mock<ILogger<PatientService>>().Object);
        // CORE-APPT-004: conversion now goes through the same atomic
        // doctor+room conflict guard the direct booking path uses.
        return new BookingRequestService(db, patientService, new AppointmentRepository(db), logger.Object);
    }

    private static Doctor CreateActiveDoctor(AppDbContext db)
    {
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            Name = "دكتور أحمد",
            IsActive = true,
            Specialty = "تقويم"
        };
        db.Doctors.Add(doctor);
        return doctor;
    }

    private static BookingRequest CreateConfirmedBookingRequest(
        AppDbContext db, Doctor? doctor = null, string? preferredDate = null, string? preferredTime = null)
    {
        var br = new BookingRequest
        {
            Id = Guid.NewGuid(),
            PatientName = "أحمد محمد",
            PhoneNumber = "967770123456",
            Status = BookingRequestStatus.Confirmed,
            DoctorId = doctor?.Id,
            PreferredDate = preferredDate ?? "2026-06-15",
            PreferredTime = preferredTime ?? "09:00",
            ServiceType = "فحص أولي"
        };
        db.BookingRequests.Add(br);
        return br;
    }

    // ─── Validator Tests ────────────────────────────────────────────────────

    public class ConvertDtoValidatorTests
    {
        private readonly ConvertBookingRequestToAppointmentDtoValidator _validator = new();

        [Fact]
        public void PatientId_EmptyGuid_ShouldPass()
        {
            // Arrange — Guid.Empty means "auto-resolve patient from booking request phone"
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: Guid.NewGuid(),
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var result = _validator.Validate(dto);

            // Assert
            result.IsValid.Should().BeTrue("PatientId may be Guid.Empty — backend auto-resolves patient");
        }

        [Fact]
        public void PatientId_NonEmptyGuid_ShouldPass()
        {
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.NewGuid(),
                DoctorId: Guid.NewGuid(),
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            var result = _validator.Validate(dto);
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void DoctorId_EmptyGuid_ShouldFail()
        {
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.NewGuid(),
                DoctorId: Guid.Empty,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            var result = _validator.Validate(dto);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "DoctorId");
        }

        [Fact]
        public void ValidDto_WithAllFields_ShouldPass()
        {
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: Guid.NewGuid(),
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(10, 0),
                EndTime: new TimeOnly(10, 30),
                AppointmentType: "تنظيف",
                DurationMinutes: 30);

            var result = _validator.Validate(dto);
            result.IsValid.Should().BeTrue();
        }
    }

    // ─── Service Integration Tests ──────────────────────────────────────────

    public class ConvertToAppointmentServiceTests
    {
        [Fact]
        public async Task ConfirmedBookingRequest_CanConvertToAppointment()
        {
            // Arrange
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = CreateConfirmedBookingRequest(db, doctor);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var result = await service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result!.ConvertedToAppointmentId.Should().NotBeNull();

            // Verify the appointment was created
            var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.DoctorId == doctor.Id);
            appointment.Should().NotBeNull();
            appointment!.PatientId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task BookingRequest_DoesNotConflictWithItself()
        {
            // Arrange — a confirmed booking request that occupies "09:00" on 2026-06-15
            // Converting it at the same time should NOT fail with a self-conflict.
            // The backend only checks for appointment conflicts, not booking-request conflicts,
            // so converting a booking request at its own preferred time should succeed
            // (no existing appointment at that time yet).
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = CreateConfirmedBookingRequest(db, doctor, "2026-06-15", "09:00");
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act — converting at the SAME time as the booking request should succeed
            var result = await service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull("a booking request should not conflict with itself during conversion");
            result!.ConvertedToAppointmentId.Should().NotBeNull();
        }

        [Fact]
        public async Task ExistingRealAppointment_BlocksConversion()
        {
            // Arrange — create a real appointment for the same doctor/date/time
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var patient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "مريض",
                LastName = "آخر",
                Phone = "967770999999",
                IsActive = true
            };
            db.Patients.Add(patient);

            var existingAppointment = new Appointment
            {
                Id = Guid.NewGuid(),
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = new DateOnly(2026, 6, 15),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                DurationMinutes = 30,
                AppointmentType = "فحص",
                Status = AppointmentStatus.Scheduled,
                IsActive = true
            };
            db.Appointments.Add(existingAppointment);

            var br = CreateConfirmedBookingRequest(db, doctor, "2026-06-15", "09:00");
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var act = () => service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert — conversion should fail due to real appointment conflict
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("يوجد موعد آخر في نفس الوقت لهذا الطبيب");
        }

        [Fact]
        public async Task AlreadyConvertedBookingRequest_CannotConvertAgain()
        {
            // Arrange — create a booking request that's already converted
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = CreateConfirmedBookingRequest(db, doctor);
            br.ConvertedToAppointmentId = Guid.NewGuid(); // already converted
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var act = () => service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert — should reject double conversion
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("تم تحويل هذا الطلب بالفعل إلى موعد");
        }

        [Fact]
        public async Task NonConfirmedBookingRequest_CannotConvert()
        {
            // Arrange — a Pending booking request should not be convertible
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = new BookingRequest
            {
                Id = Guid.NewGuid(),
                PatientName = "سعيد علي",
                PhoneNumber = "967770555555",
                Status = BookingRequestStatus.Pending,
                DoctorId = doctor.Id,
                PreferredDate = "2026-06-15",
                PreferredTime = "10:00"
            };
            db.BookingRequests.Add(br);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(10, 0),
                EndTime: new TimeOnly(10, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var act = () => service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("يجب أن يكون الطلب في حالة مؤكد لتحويله إلى موعد");
        }

        [Fact]
        public async Task Conversion_SetsConvertedToAppointmentId()
        {
            // Arrange
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = CreateConfirmedBookingRequest(db, doctor);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var result = await service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result!.ConvertedToAppointmentId.Should().NotBeNull();

            // Also verify the appointment was actually created
            var appointmentId = result.ConvertedToAppointmentId!.Value;
            var createdAppointment = await db.Appointments
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
            createdAppointment.Should().NotBeNull();
            createdAppointment!.DoctorId.Should().Be(doctor.Id);
        }

        [Fact]
        public async Task Conversion_AutoCreatesPatient_WhenPatientIdIsEmpty()
        {
            // Arrange — no existing patient with this phone
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var br = CreateConfirmedBookingRequest(db, doctor);
            br.PatientName = "خالد سالم";
            br.PhoneNumber = "967771234567";
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty, // auto-resolve
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            // Act
            var result = await service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            // Assert — a patient should have been auto-created
            result.Should().NotBeNull();
            var newPatient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == "967771234567");
            newPatient.Should().NotBeNull("patient should be auto-created from booking request data");
            newPatient!.FirstName.Should().Be("خالد");
            newPatient.LastName.Should().Be("سالم");
        }

        // ── CORE-APPT-004: conversion must honor the room conflict guard ──────
        // This path stored ClinicRoomId but never checked it at all, so a booking
        // request could be converted straight into a room another doctor already
        // occupied.

        [Fact]
        public async Task Conversion_RejectsRoomConflict_EvenForADifferentDoctor()
        {
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var otherDoctor = new Doctor { Id = Guid.NewGuid(), Name = "دكتور آخر", IsActive = true, Specialty = "عام" };
            db.Doctors.Add(otherDoctor);

            var roomId = Guid.NewGuid();
            db.Appointments.Add(new Appointment
            {
                Id = Guid.NewGuid(),
                DoctorId = otherDoctor.Id,
                PatientId = Guid.NewGuid(),
                ClinicRoomId = roomId,
                AppointmentDate = new DateOnly(2026, 6, 15),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                DurationMinutes = 30,
                AppointmentType = "فحص",
                Status = AppointmentStatus.Scheduled,
                IsActive = true
            });

            var br = CreateConfirmedBookingRequest(db, doctor, "2026-06-15", "09:00");
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,           // different doctor — only the ROOM conflicts
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30,
                ClinicRoomId: roomId);

            var act = () => service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("الغرفة محجوزة في هذا الوقت");

            var reloaded = await db.BookingRequests.AsNoTracking().FirstAsync(r => r.Id == br.Id);
            reloaded.ConvertedToAppointmentId.Should().BeNull("a rejected conversion must not mark the request converted");
        }

        [Fact]
        public async Task Conversion_AllowsSameRoom_WhenTimesDoNotOverlap()
        {
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            var otherDoctor = new Doctor { Id = Guid.NewGuid(), Name = "دكتور آخر", IsActive = true, Specialty = "عام" };
            db.Doctors.Add(otherDoctor);

            var roomId = Guid.NewGuid();
            db.Appointments.Add(new Appointment
            {
                Id = Guid.NewGuid(),
                DoctorId = otherDoctor.Id,
                PatientId = Guid.NewGuid(),
                ClinicRoomId = roomId,
                AppointmentDate = new DateOnly(2026, 6, 15),
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(11, 30),
                DurationMinutes = 30,
                AppointmentType = "فحص",
                Status = AppointmentStatus.Scheduled,
                IsActive = true
            });

            var br = CreateConfirmedBookingRequest(db, doctor, "2026-06-15", "09:00");
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30,
                ClinicRoomId: roomId);

            var result = await service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());

            result.Should().NotBeNull();
            result!.ConvertedToAppointmentId.Should().NotBeNull();
        }

        [Fact]
        public async Task Conversion_RejectedByDoctorConflict_DoesNotMarkRequestConverted()
        {
            // The pre-existing doctor-conflict rejection now happens inside the
            // guarded transaction — the request must stay unconverted and no
            // appointment row may be left behind.
            using var db = CreateInMemoryDb();
            var doctor = CreateActiveDoctor(db);
            db.Appointments.Add(new Appointment
            {
                Id = Guid.NewGuid(),
                DoctorId = doctor.Id,
                PatientId = Guid.NewGuid(),
                AppointmentDate = new DateOnly(2026, 6, 15),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                DurationMinutes = 30,
                AppointmentType = "فحص",
                Status = AppointmentStatus.Scheduled,
                IsActive = true
            });
            var br = CreateConfirmedBookingRequest(db, doctor, "2026-06-15", "09:00");
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var dto = new ConvertBookingRequestToAppointmentDto(
                PatientId: Guid.Empty,
                DoctorId: doctor.Id,
                AppointmentDate: new DateOnly(2026, 6, 15),
                StartTime: new TimeOnly(9, 0),
                EndTime: new TimeOnly(9, 30),
                AppointmentType: "فحص أولي",
                DurationMinutes: 30);

            var act = () => service.ConvertToAppointmentAsync(br.Id, dto, Guid.NewGuid());
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("يوجد موعد آخر في نفس الوقت لهذا الطبيب");

            var reloaded = await db.BookingRequests.AsNoTracking().FirstAsync(r => r.Id == br.Id);
            reloaded.ConvertedToAppointmentId.Should().BeNull();
            (await db.Appointments.CountAsync()).Should().Be(1, "no second appointment may be created");
        }
    }
}

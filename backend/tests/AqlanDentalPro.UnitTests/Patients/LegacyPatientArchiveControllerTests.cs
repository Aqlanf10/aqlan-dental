using System.Reflection;
using System.Text.Json;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class LegacyPatientArchiveControllerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Controller_IsRestrictedToAdminPolicy()
    {
        var attribute = typeof(LegacyPatientArchiveController)
            .GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Policy.Should().Be("AdminOnly");
    }

    [Fact]
    public async Task Get_ReturnsArchivedContent_WithoutCreatingLiveFinancialOrSchedulingRecords()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "OLD-01",
            FirstName = "Legacy",
            LastName = "Patient"
        };
        db.Patients.Add(patient);
        db.LegacyAppointmentArchives.Add(new LegacyAppointmentArchive
        {
            Patient = patient,
            SourceAppointmentId = "appointment-1",
            AppointmentAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            ArchiveType = "Legacy appointment"
        });
        db.LegacyTreatmentArchives.Add(new LegacyTreatmentArchive
        {
            Patient = patient,
            SourceLineId = "treatment-1",
            LineTotal = 100m
        });
        db.LegacyFinancialArchiveEntries.Add(new LegacyFinancialArchiveEntry
        {
            Patient = patient,
            SourceEntryId = "finance-1",
            DebitAmount = 20m,
            CreditAmount = 10m
        });
        db.LegacyLinkedArchiveRecords.Add(new LegacyLinkedArchiveRecord
        {
            Patient = patient,
            SourceTable = "TBL092",
            SourceRecordId = "record-1"
        });
        await db.SaveChangesAsync();

        var result = await new LegacyPatientArchiveController(db).Get(patient.Id);

        result.Should().BeOfType<OkObjectResult>();
        var payload = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        payload.Should().Contain("\"appointmentCards\":1");
        payload.Should().Contain("\"treatmentLines\":1");
        payload.Should().Contain("\"financialEntryLines\":1");
        payload.Should().Contain("\"unclassifiedLinkedRecords\":1");
        payload.Should().Contain("\"balanceAffecting\":false");
        (await db.Appointments.CountAsync()).Should().Be(0);
        (await db.Payments.CountAsync()).Should().Be(0);
        (await db.Contracts.CountAsync()).Should().Be(0);
        (await db.Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Get_UnknownPatient_ReturnsNotFound()
    {
        await using var db = CreateDb();

        var result = await new LegacyPatientArchiveController(db).Get(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

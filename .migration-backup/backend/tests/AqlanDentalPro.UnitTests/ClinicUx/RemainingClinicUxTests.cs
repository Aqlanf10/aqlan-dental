using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicUx;

public class RemainingClinicUxTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task DocumentTemplate_Render_ReplacesPatientPlaceholders()
    {
        using var db = CreateDb();
        var patient = new Patient
        {
            FirstName = "TEST",
            LastName = "PATIENT",
            PatientNumber = "P-TEST-001",
            Phone = "770000000",
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var controller = new DocumentTemplatesController(db);
        var create = await controller.Create(new UpsertDocumentTemplateRequest
        {
            Title = "Consent",
            Category = "consent",
            Content = "Patient: {{PatientName}} / File: {{PatientFileNumber}} / Phone: {{PatientPhone}}",
            RequiresSignature = true,
            IsActive = true,
        });

        var created = create.Should().BeOfType<CreatedAtActionResult>().Subject;
        var createdId = (Guid)created.Value!.GetType().GetProperty("Id")!.GetValue(created.Value)!;

        var renderedResult = await controller.Render(createdId, new RenderDocumentTemplateRequest
        {
            PatientId = patient.Id
        });

        var ok = renderedResult.Should().BeOfType<OkObjectResult>().Subject;
        var content = ok.Value!.GetType().GetProperty("Content")!.GetValue(ok.Value)!.ToString();
        content.Should().Contain("TEST PATIENT");
        content.Should().Contain("P-TEST-001");
        content.Should().Contain("770000000");
    }

    [Fact]
    public async Task ConsumeServiceInventory_DecrementsItemsAndWritesAdjustment()
    {
        using var db = CreateDb();
        var service = new ClinicService
        {
            ArabicName = "حشوة تجريبية",
            EnglishName = "Test Filling",
            Code = "TEST-FILL"
        };
        var item = new Inventory
        {
            Name = "Composite",
            Quantity = 10,
            MinQuantity = 2,
            Unit = "unit"
        };
        db.ClinicServices.Add(service);
        db.Inventory.Add(item);
        await db.SaveChangesAsync();

        db.ServiceConsumables.Add(new ServiceConsumable
        {
            ClinicServiceId = service.Id,
            InventoryItemId = item.Id,
            Quantity = 2
        });
        await db.SaveChangesAsync();

        var controller = new InventoryController(db, NullLogger<InventoryController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                    ], "Test"))
                }
            }
        };
        var result = await controller.ConsumeServiceInventory(new ConsumeServiceInventoryRequest
        {
            ServiceId = service.Id,
            Quantity = 3,
            Notes = "unit test"
        });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Inventory.FindAsync(item.Id);
        updated!.Quantity.Should().Be(4);

        var adjustment = await db.InventoryAdjustments.SingleAsync();
        adjustment.InventoryItemId.Should().Be(item.Id);
        adjustment.Delta.Should().Be(-6);
        adjustment.PreviousQuantity.Should().Be(10);
        adjustment.NewQuantity.Should().Be(4);
        adjustment.AdjustmentType.Should().Be("consumption");
    }
}

using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class ActiveOrthoCaseDefinitionTests
{
    [Fact]
    public async Task ActiveCases_RequiresBothSoftDeleteAndClinicalActiveStatus()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var patientId = Guid.NewGuid();

        db.OrthoCases.AddRange(
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-ACTIVE",
                Status = OrthoCaseStatus.Active,
                IsActive = true,
            },
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-COMPLETED",
                Status = OrthoCaseStatus.Completed,
                IsActive = true,
            },
            new OrthoCase
            {
                PatientId = patientId,
                CaseNumber = "ORTHO-DELETED",
                Status = OrthoCaseStatus.Active,
                IsActive = false,
            });
        await db.SaveChangesAsync();

        var result = await db.OrthoCases
            .IgnoreQueryFilters()
            .ActiveCases()
            .Select(c => c.CaseNumber)
            .ToListAsync();

        result.Should().Equal("ORTHO-ACTIVE");
    }
}

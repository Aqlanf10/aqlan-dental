using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Employees;

public class EmployeesControllerListTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetAll_OrphanEmployeeRecord_DoesNotBreakList()
    {
        await using var db = CreateDb();

        var user = new User
        {
            Username = "linked-employee",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.Reception
        };

        db.Users.Add(user);
        db.Employees.Add(new Employee
        {
            UserId = user.Id,
            User = user,
            FullName = "Linked Employee",
            Position = "Reception"
        });

        db.Employees.Add(new Employee
        {
            UserId = Guid.NewGuid(),
            FullName = "Orphan Employee",
            Position = "Legacy"
        });

        await db.SaveChangesAsync();

        var controller = new EmployeesController(db, NullLogger<EmployeesController>.Instance);

        var result = await controller.GetAll(search: null, role: null, branchId: null, page: 1, pageSize: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        var value = ok.Value!;
        var total = (int)value.GetType().GetProperty("total")!.GetValue(value)!;
        var data = (IEnumerable<object>)value.GetType().GetProperty("data")!.GetValue(value)!;

        total.Should().Be(1);
        data.Should().ContainSingle();
    }
}

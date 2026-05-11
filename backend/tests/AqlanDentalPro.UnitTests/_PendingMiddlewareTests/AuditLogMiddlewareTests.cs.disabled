using AqlanDentalPro.API.Middleware;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace AqlanDentalPro.UnitTests.Middleware;

public class AuditLogMiddlewareTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DefaultHttpContext CreateHttpContext(
        string method = "POST",
        string path = "/api/patients",
        ClaimsPrincipal? user = null,
        int statusCode = 200)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.StatusCode = statusCode;

        if (user != null)
        {
            context.User = user;
        }

        // Set up connection for RemoteIpAddress
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        return context;
    }

    private static ClaimsPrincipal CreateStaffUser(Guid userId, string role = "Admin")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "staffuser"),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePatientPortalUser(Guid patientAccountId, string username = "GM-2026-009")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patientAccountId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "Patient"),
            new("portal", "true"),
            new("patientId", Guid.NewGuid().ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid? userId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(x => x.UserId).Returns(userId);
        mock.SetupGet(x => x.IsAuthenticated).Returns(userId.HasValue);
        return mock;
    }

    [Fact]
    public async Task StaffUser_AuditLog_StoresUserId()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var staffUserId = Guid.NewGuid();
        var user = CreateStaffUser(staffUserId);
        var context = CreateHttpContext("POST", "/api/patients", user);
        var currentUser = CreateCurrentUserService(staffUserId);

        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        nextCalled.Should().BeTrue();
        var auditLogs = await db.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);
        auditLogs[0].UserId.Should().Be(staffUserId);
        auditLogs[0].Action.Should().Be(AuditAction.Create);
        auditLogs[0].Resource.Should().Be("patients");
        auditLogs[0].NewData.Should().BeNull(); // Staff users don't get metadata in NewData
    }

    [Fact]
    public async Task PatientPortalUser_AuditLog_DoesNotStoreUserIdInFK()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var patientAccountId = Guid.NewGuid(); // This ID is in PatientAccounts, NOT Users table
        var user = CreatePatientPortalUser(patientAccountId);
        var context = CreateHttpContext("POST", "/api/portal/messages/conversations", user);
        var currentUser = CreateCurrentUserService(patientAccountId);

        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        nextCalled.Should().BeTrue();
        var auditLogs = await db.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);
        auditLogs[0].UserId.Should().BeNull(); // Must be null to avoid FK violation
    }

    [Fact]
    public async Task PatientPortalUser_AuditLog_StoresIdentityInNewData()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var patientAccountId = Guid.NewGuid();
        var user = CreatePatientPortalUser(patientAccountId, "GM-2026-009");
        var context = CreateHttpContext("POST", "/api/portal/messages/conversations", user);
        var currentUser = CreateCurrentUserService(patientAccountId);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        var auditLog = await db.AuditLogs.FirstAsync();
        auditLog.NewData.Should().NotBeNull();
        auditLog.NewData!.RootElement.GetProperty("actorType").GetString().Should().Be("PatientPortal");
        auditLog.NewData.RootElement.GetProperty("patientUsername").GetString().Should().Be("GM-2026-009");
    }

    [Fact]
    public async Task PatientPortalUser_Messaging_DoesNotFailDueToAuditLogFK()
    {
        // This test simulates the exact production bug scenario:
        // Patient portal user sends a message → audit logging fails with FK violation.
        // With the fix, audit log UserId is null, so no FK violation occurs.

        // Arrange
        var db = CreateInMemoryDb();

        // Add a User to the DB so we can verify staff audit logging still works
        var staffUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = staffUserId,
            Username = "admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt"
        });
        await db.SaveChangesAsync();

        var patientAccountId = Guid.NewGuid(); // NOT in Users table
        var user = CreatePatientPortalUser(patientAccountId);
        var context = CreateHttpContext("POST", "/api/portal/messages/conversations", user);
        var currentUser = CreateCurrentUserService(patientAccountId);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act — should NOT throw DbUpdateException
        var act = () => middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        await act.Should().NotThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
        var auditLog = await db.AuditLogs.FirstAsync();
        auditLog.UserId.Should().BeNull();
    }

    [Fact]
    public async Task AdminUser_AuditLoggingStillWorksAfterFix()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var adminUserId = Guid.NewGuid();

        // Add the admin user to the Users table
        db.Users.Add(new User
        {
            Id = adminUserId,
            Username = "admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt"
        });
        await db.SaveChangesAsync();

        var user = CreateStaffUser(adminUserId, "Admin");
        var context = CreateHttpContext("PUT", "/api/patients/some-id", user);
        var currentUser = CreateCurrentUserService(adminUserId);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        var auditLog = await db.AuditLogs.FirstAsync();
        auditLog.UserId.Should().Be(adminUserId);
        auditLog.Action.Should().Be(AuditAction.Update);
        auditLog.NewData.Should().BeNull(); // No metadata for staff users
    }

    [Fact]
    public async Task UnauthenticatedUser_AuditLogNotCreated()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity()); // No auth type
        var context = CreateHttpContext("POST", "/api/patients", unauthenticatedUser);
        var currentUser = CreateCurrentUserService(null);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        var auditLogs = await db.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task PatientPortalUser_WithOnlyRoleClaim_DetectedCorrectly()
    {
        // Test fallback detection via Role claim only (no "portal" claim)
        // Arrange
        var db = CreateInMemoryDb();
        var patientAccountId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patientAccountId.ToString()),
            new(ClaimTypes.Role, "Patient"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var context = CreateHttpContext("POST", "/api/portal/messages/conversations", user);
        var currentUser = CreateCurrentUserService(patientAccountId);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        var auditLog = await db.AuditLogs.FirstAsync();
        auditLog.UserId.Should().BeNull(); // Still detected as patient portal
    }

    [Fact]
    public async Task StaffWithReceptionRole_StillLogsUserId()
    {
        // Ensure Reception role (not Patient) still logs UserId normally
        // Arrange
        var db = CreateInMemoryDb();
        var receptionUserId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = receptionUserId,
            Username = "reception1",
            Role = UserRole.Reception,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt"
        });
        await db.SaveChangesAsync();

        var user = CreateStaffUser(receptionUserId, "Reception");
        var context = CreateHttpContext("POST", "/api/appointments", user);
        var currentUser = CreateCurrentUserService(receptionUserId);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new AuditLogMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, db, currentUser.Object);

        // Assert
        var auditLog = await db.AuditLogs.FirstAsync();
        auditLog.UserId.Should().Be(receptionUserId);
    }
}

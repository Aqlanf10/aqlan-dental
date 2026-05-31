using System.Reflection;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

/// <summary>
/// Tests for PR #202 blockers:
///   Blocker 1 — Access-query failure must return HTTP 500, not HTTP 200 empty list.
///   Blocker 2 — Legitimate empty doctor list must return valid pagination metadata.
/// </summary>
public class PatientsControllerListTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateCurrentUser(Guid userId, UserRole role)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static IPatientAccessService CreatePatientAccess(
        bool isDoctor, HashSet<Guid>? accessibleIds, bool shouldThrow = false)
    {
        var mock = new Mock<IPatientAccessService>();
        mock.Setup(p => p.IsDoctor).Returns(isDoctor);
        mock.Setup(p => p.HasFullAccess).Returns(!isDoctor);

        if (shouldThrow)
        {
            mock.Setup(p => p.GetAccessiblePatientIdsAsync())
                .ThrowsAsync(new InvalidOperationException("Simulated DB failure: relation does not exist"));
        }
        else
        {
            mock.Setup(p => p.GetAccessiblePatientIdsAsync())
                .ReturnsAsync(accessibleIds);
        }

        return mock.Object;
    }

    /// <summary>
    /// Constructs the controller with only the dependencies needed for GetList
    /// doctor-branch paths. Unused services are passed as null since the tested
    /// code paths never access them.
    /// </summary>
    private static PatientsController BuildController(
        ICurrentUserService currentUser,
        IPatientAccessService patientAccess,
        ILogger<PatientsController>? logger = null)
    {
        var db = CreateDb();
        var mockPortal = new Mock<IPatientPortalService>().Object;
        var mockAudit = new Mock<IAuditService>().Object;
        logger ??= new Mock<ILogger<PatientsController>>().Object;

        // PatientService and FinanceService are not invoked on the doctor-empty
        // or access-failure paths, so we pass null — the primary constructor
        // simply stores them and they are never dereferenced in these code paths.
        return new PatientsController(
            service: null!,
            db: db,
            portalService: mockPortal,
            financeService: null!,
            currentUser: currentUser,
            patientAccess: patientAccess,
            audit: mockAudit,
            logger: logger);
    }

    // ── Test 1: Doctor with no accessible patients → HTTP 200 + valid empty response ──

    [Fact]
    public async Task GetList_DoctorNoAccessiblePatients_ReturnsHttp200WithValidEmptyResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId, UserRole.GeneralDentist);
        var patientAccess = CreatePatientAccess(isDoctor: true, accessibleIds: new HashSet<Guid>());
        var controller = BuildController(currentUser, patientAccess);

        // Act
        var result = await controller.GetList(search: null, page: 1, pageSize: 20);

        // Assert — must be HTTP 200 (not 500)
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.StatusCode.Should().Be(200);

        // Response must be a PaginatedResponse<PatientListDto>
        var response = ok.Value.Should().BeOfType<PaginatedResponse<PatientListDto>>().Subject;
        response.Data.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    // ── Test 2: Empty response has valid pagination metadata ──

    [Fact]
    public async Task GetList_DoctorNoAccessiblePatients_ValidPaginationMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId, UserRole.Orthodontist);
        var patientAccess = CreatePatientAccess(isDoctor: true, accessibleIds: new HashSet<Guid>());
        var controller = BuildController(currentUser, patientAccess);

        // Act
        var result = await controller.GetList(search: null, page: 1, pageSize: 20);

        // Assert — pagination metadata must be valid
        var ok = (OkObjectResult)result;
        var response = (PaginatedResponse<PatientListDto>)ok.Value!;

        response.Page.Should().BeGreaterThanOrEqualTo(1, "page must be >= 1 for valid pagination");
        response.PageSize.Should().BeGreaterThan(0, "pageSize must be > 0 to avoid division by zero");
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0, "0 items / any pageSize = 0 pages");
        response.HasNextPage.Should().BeFalse();
        response.HasPreviousPage.Should().BeFalse();
    }

    // ── Test 2b: Empty response normalizes page/pageSize even for bad inputs ──

    [Theory]
    [InlineData(0, 0, 1, 1)]     // zero page/size → normalized to 1/1
    [InlineData(-1, -5, 1, 1)]   // negative → normalized to 1/1
    [InlineData(1, 200, 1, 100)] // pageSize > 100 → clamped to 100
    [InlineData(3, 50, 3, 50)]   // valid values preserved
    public async Task GetList_DoctorNoPatients_NormalizesPageAndPageSize(
        int inputPage, int inputPageSize, int expectedPage, int expectedPageSize)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId, UserRole.OralSurgeon);
        var patientAccess = CreatePatientAccess(isDoctor: true, accessibleIds: new HashSet<Guid>());
        var controller = BuildController(currentUser, patientAccess);

        // Act
        var result = await controller.GetList(search: null, page: inputPage, pageSize: inputPageSize);

        // Assert
        var ok = (OkObjectResult)result;
        var response = (PaginatedResponse<PatientListDto>)ok.Value!;

        response.Page.Should().Be(expectedPage);
        response.PageSize.Should().Be(expectedPageSize);
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0);
    }

    // ── Test 3: Access-query failure returns HTTP 500, not HTTP 200 ──

    [Fact]
    public async Task GetList_AccessQueryFailure_ReturnsHttp500_GenericMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId, UserRole.GeneralDentist);
        var patientAccess = CreatePatientAccess(isDoctor: true, accessibleIds: null, shouldThrow: true);
        var logger = new Mock<ILogger<PatientsController>>();
        var controller = BuildController(currentUser, patientAccess, logger.Object);

        // Act
        var result = await controller.GetList(search: null, page: 1, pageSize: 20);

        // Assert — must be HTTP 500, NOT HTTP 200
        result.Should().BeOfType<ObjectResult>("access-query failure must return 500, not 200");
        var error = (ObjectResult)result;
        error.StatusCode.Should().Be(500);

        // Must contain the generic Arabic message — check the anonymous object's message property
        var errorObj = error.Value!;
        var msgProp = errorObj.GetType().GetProperty("message");
        msgProp.Should().NotBeNull("error response must have a 'message' property");
        msgProp!.GetValue(errorObj)!.Should().Be("تعذر تحميل بيانات المرضى حالياً");

        // Exception was logged server-side
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("GetAccessiblePatientIdsAsync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Exception must be logged server-side before returning 500");
    }

    // ── Test 4: Error response contains no sensitive details ──

    [Fact]
    public async Task GetList_AccessQueryFailure_ErrorResponseContainsNoSensitiveDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId, UserRole.Orthodontist);
        var patientAccess = CreatePatientAccess(isDoctor: true, accessibleIds: null, shouldThrow: true);
        var controller = BuildController(currentUser, patientAccess);

        // Act
        var result = await controller.GetList(search: null, page: 1, pageSize: 20);
        var error = (ObjectResult)result;

        // Assert — response must not leak any sensitive information
        // Check via reflection: the anonymous object should only have a "message" property.
        var errorObj = error.Value!;
        var properties = errorObj.GetType().GetProperties();
        var propertyNames = properties.Select(p => p.Name.ToLowerInvariant()).ToList();

        propertyNames.Should().NotContain("stacktrace", "error response must not contain stack traces");
        propertyNames.Should().NotContain("innermessage", "error response must not contain inner exception messages");
        propertyNames.Should().NotContain("innerexception", "error response must not contain inner exceptions");
        propertyNames.Should().NotContain("sql", "error response must not contain SQL details");
        propertyNames.Should().NotContain("exceptiontype", "error response must not contain exception types");
        propertyNames.Should().NotContain("detail", "error response must not contain raw exception messages");
        propertyNames.Should().NotContain("dbupdate", "error response must not contain EF exception types");
        propertyNames.Should().NotContain("npgsql", "error response must not contain database provider names");

        // Verify no property value contains exception-related strings
        foreach (var prop in properties)
        {
            var val = prop.GetValue(errorObj)?.ToString()?.ToLowerInvariant() ?? "";
            val.Should().NotContain("invalidoperationexception", "property values must not contain exception types");
            val.Should().NotContain("relation does not exist", "property values must not contain raw SQL errors");
            val.Should().NotContain("stack", "property values must not contain stack trace info");
        }

        // The only property should be "message" with the generic Arabic response
        properties.Should().ContainSingle(p => p.Name == "message",
            "error response must only contain the generic Arabic message property");
        var msgProp = properties.First(p => p.Name == "message");
        msgProp.GetValue(errorObj).Should().Be("تعذر تحميل بيانات المرضى حالياً");
    }

    // ── Test 5: PaginatedResponse default constructor has invalid metadata (regression guard) ──

    [Fact]
    public void PaginatedResponse_DefaultConstructor_HasInvalidPaginationMetadata()
    {
        // This test documents why we CANNOT use `new PaginatedResponse<T>()` for
        // empty responses — the default constructor leaves Page=0, PageSize=0,
        // which causes TotalPages = NaN (0/0) and breaks frontend pagination.

        var response = new PaginatedResponse<PatientListDto>();

        response.Page.Should().Be(0, "default Page is 0 — invalid for pagination");
        response.PageSize.Should().Be(0, "default PageSize is 0 — causes division by zero");

        // TotalPages = Math.Ceiling(0.0 / 0.0) = NaN in floating point
        double totalPagesCalc = (double)response.TotalCount / response.PageSize;
        totalPagesCalc.Should().Be(double.NaN, "0/0 = NaN — would break JSON serialization");
    }

    // ── Test 6: PaginatedResponse with normalized values has correct computed properties ──

    [Fact]
    public void PaginatedResponse_NormalizedEmpty_HasValidComputedProperties()
    {
        // This test validates the correct construction used in the fix.
        var response = new PaginatedResponse<PatientListDto>
        {
            Data = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        response.TotalPages.Should().Be(0, "0 items / 20 pageSize = 0 pages");
        response.HasNextPage.Should().BeFalse();
        response.HasPreviousPage.Should().BeFalse();
        response.Data.Should().BeEmpty();
    }
}

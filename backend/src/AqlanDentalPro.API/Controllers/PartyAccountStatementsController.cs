using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/finance-v3/party-statements")]
[Authorize(Policy = "ReportsAccess")]
public sealed class PartyAccountStatementsController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPartyAccountStatementService statements) : ControllerBase
{
    [HttpGet("{partyType}/{partyId:guid}")]
    public async Task<IActionResult> Get(
        string partyType,
        Guid partyId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
    {
        if (!await PermissionGuard.HasAsync(db, currentUser, "finance.account_statement", "view"))
            return StatusCode(403, new { message = "غير مصرح لك بعرض كشوف الحساب" });

        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId == Guid.Empty)
                return StatusCode(403, new { message = "ليس لديك فرع معين" });
            branchId = currentUser.BranchId;
        }

        try
        {
            var result = await statements.GetAsync(
                partyType, partyId, branchId, currency, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.Application.DTOs.Credits;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

// Not flag-gated: credits already granted stay spendable and visible if the referrals flag is turned off.
[ApiVersion("1.0")]
[ApiController]
[Authorize]
public class AccountCreditsController : BaseApiController
{
    private readonly IAccountCreditService _accountCreditService;
    private readonly IAuditLogService _auditLogService;

    public AccountCreditsController(IAccountCreditService accountCreditService, IAuditLogService auditLogService)
    {
        _accountCreditService = accountCreditService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// The caller's credit balance and ledger, newest first
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/users/me/credits")]
    [ProducesResponseType(typeof(AccountCreditsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountCreditsDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _accountCreditService.GetForUserAsync(userId.Value));
    }

    /// <summary>
    /// A player's credit balance and ledger (Admin only)
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/admin/users/{userId:guid}/credits")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AccountCreditsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountCreditsDto>> GetForUser(Guid userId) =>
        Ok(await _accountCreditService.GetForUserAsync(userId));

    /// <summary>
    /// Add or remove a player's credit with a note (Admin only). Audited.
    /// </summary>
    [HttpPost("api/v{version:apiVersion}/admin/users/{userId:guid}/credits")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AccountCreditsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCreditsDto>> Adjust(Guid userId, [FromBody] AdjustAccountCreditDto request)
    {
        try
        {
            var result = await _accountCreditService.AdjustAsync(userId, request.Amount, request.Note, User.AuditUserId());
            await _auditLogService.LogAsync(request.Amount > 0 ? "CreditAdded" : "CreditRemoved", "User", userId,
                $"{request.Amount:+0.00;-0.00} credit. Note: {request.Note?.Trim()}. New balance: {result.Balance:0.00}",
                User.AuditUserId(), User.AuditUserName());
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

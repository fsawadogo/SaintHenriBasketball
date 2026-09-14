using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Credits;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

// Not flag-gated: credits already granted stay spendable and visible if the referrals flag is turned off.
[ApiVersion("1.0")]
[ApiController]
[Authorize]
public class AccountCreditsController : BaseApiController
{
    private readonly IAccountCreditService _accountCreditService;

    public AccountCreditsController(IAccountCreditService accountCreditService)
    {
        _accountCreditService = accountCreditService;
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
}

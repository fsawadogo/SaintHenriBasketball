using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// Clearing out signups that never confirmed an address and never did anything.
///
/// Two steps, because it deletes people: a preview that names every account and removes nothing,
/// then the purge. Anything with a payment, booking, attendance or waitlist place is kept either
/// way — that is a real person, not litter.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/users/unconfirmed")]
[Authorize(Roles = "Admin")]
public class UnconfirmedAccountsController : ControllerBase
{
    private readonly IUnconfirmedAccountPurgeService _purge;
    private readonly IAuditLogService _audit;

    public UnconfirmedAccountsController(IUnconfirmedAccountPurgeService purge, IAuditLogService audit)
    {
        _purge = purge;
        _audit = audit;
    }

    /// <summary>Who would be removed. Removes nothing.</summary>
    [HttpGet("purgeable")]
    [ProducesResponseType(typeof(UnconfirmedPurgeResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnconfirmedPurgeResultDto>> Purgeable([FromQuery] int olderThanDays = 7) =>
        Ok(await _purge.RunAsync(olderThanDays, dryRun: true));

    /// <summary>Removes them. Audited with the count, because it deletes accounts.</summary>
    [HttpPost("purge")]
    [ProducesResponseType(typeof(UnconfirmedPurgeResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnconfirmedPurgeResultDto>> Purge([FromQuery] int olderThanDays = 7)
    {
        var result = await _purge.RunAsync(olderThanDays, dryRun: false);

        await _audit.LogAsync(
            "UnconfirmedAccountsPurged", "User", null,
            $"Removed {result.Deleted} never-confirmed account(s) older than {olderThanDays} day(s); {result.KeptWithHistory} kept for having history",
            User.AuditUserId(), User.AuditUserName());

        return Ok(result);
    }
}

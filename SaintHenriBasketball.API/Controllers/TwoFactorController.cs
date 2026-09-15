using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.TwoFactor;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

// Only `verify` accepts a 2FA-pending (password-only) token. Setup and confirm also accept a required-setup
// token (an admin without 2FA while admin-2fa is on); they refuse to replace an authenticator that is on.
// Disable needs a fully verified session and a current code.
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/auth/2fa")]
[Authorize]
[RequireFeature(FeatureFlagKeys.Admin2fa)]
public class TwoFactorController : BaseApiController
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLogService;

    public TwoFactorController(ITwoFactorService twoFactorService, IUserService userService, IAuditLogService auditLogService)
    {
        _twoFactorService = twoFactorService;
        _userService = userService;
        _auditLogService = auditLogService;
    }

    [HttpPost("setup")]
    [AllowTwoFactorEnrollment]
    [ProducesResponseType(typeof(TwoFactorSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TwoFactorSetupDto>> BeginSetup()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var setup = await _twoFactorService.BeginSetupAsync(userId.Value);
            return Ok(setup);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Confirms the authenticator. For a required-setup session, the response carries a full session token.
    /// </summary>
    [HttpPost("confirm")]
    [AllowTwoFactorEnrollment]
    [ProducesResponseType(typeof(TwoFactorVerifyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm([FromBody] TwoFactorCodeDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _twoFactorService.ConfirmSetupAsync(userId.Value, body.Code);
            await _auditLogService.LogAsync("TwoFactorEnabled", "User", userId, "Two-factor authentication turned on", userId, User.AuditUserName());
            if (User.HasClaim("2fa_enroll", "true"))
                return Ok(new TwoFactorVerifyResultDto { Token = await _userService.IssueTokenAsync(userId.Value) });
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("verify")]
    [SkipTwoFactorPendingCheck]
    [ProducesResponseType(typeof(TwoFactorVerifyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TwoFactorVerifyResultDto>> Verify([FromBody] TwoFactorCodeDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var ok = await _twoFactorService.VerifyCodeAsync(userId.Value, body.Code);
        if (!ok) return BadRequest("Invalid verification code");

        var token = await _userService.IssueTokenAsync(userId.Value, twoFactorPending: false);
        return Ok(new TwoFactorVerifyResultDto { Token = token });
    }

    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable([FromBody] TwoFactorCodeDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _twoFactorService.DisableAsync(userId.Value, body.Code);
            await _auditLogService.LogAsync("TwoFactorDisabled", "User", userId, "Two-factor authentication turned off", userId, User.AuditUserName());
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

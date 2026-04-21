using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.TwoFactor;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/auth/2fa")]
[Authorize]
[RequireFeature(FeatureFlagKeys.Admin2fa)]
[SkipTwoFactorPendingCheck]
public class TwoFactorController : BaseApiController
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly IUserService _userService;

    public TwoFactorController(ITwoFactorService twoFactorService, IUserService userService)
    {
        _twoFactorService = twoFactorService;
        _userService = userService;
    }

    [HttpPost("setup")]
    [ProducesResponseType(typeof(TwoFactorSetupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorSetupDto>> BeginSetup()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var setup = await _twoFactorService.BeginSetupAsync(userId.Value);
            return Ok(setup);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm([FromBody] TwoFactorCodeDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _twoFactorService.ConfirmSetupAsync(userId.Value, body.Code);
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("verify")]
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
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

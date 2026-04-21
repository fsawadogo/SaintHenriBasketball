using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Sms;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.SmsReminders)]
public class SmsPreferencesController : BaseApiController
{
    private readonly IUserRepository _userRepository;

    public SmsPreferencesController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/sms-preferences")]
    [ProducesResponseType(typeof(SmsPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SmsPreferenceDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        return Ok(new SmsPreferenceDto { SmsOptIn = user.SmsOptIn, PhoneNumber = user.PhoneNumber });
    }

    [HttpPut("api/v{version:apiVersion}/users/me/sms-preferences")]
    [ProducesResponseType(typeof(SmsPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SmsPreferenceDto>> UpdateOwn([FromBody] SmsPreferenceDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();

        user.PhoneNumber = string.IsNullOrWhiteSpace(body.PhoneNumber) ? null : body.PhoneNumber.Trim();
        user.SmsOptIn = body.SmsOptIn && !string.IsNullOrEmpty(user.PhoneNumber);
        await _userRepository.UpdateAsync(user);
        return Ok(new SmsPreferenceDto { SmsOptIn = user.SmsOptIn, PhoneNumber = user.PhoneNumber });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Sms;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.SmsReminders)]
public class SmsPreferencesController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly ISmsService _smsService;

    public SmsPreferencesController(IUserRepository userRepository, ISmsService smsService)
    {
        _userRepository = userRepository;
        _smsService = smsService;
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

    [HttpPost("api/v{version:apiVersion}/users/me/sms-preferences/test")]
    [ProducesResponseType(typeof(SmsTestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SmsTestResultDto>> SendTest()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(user.PhoneNumber))
            return BadRequest("Save a phone number first.");

        var providerType = _smsService.GetType().Name;
        var configured = _smsService.IsConfigured;
        var sent = await _smsService.SendAsync(
            user.PhoneNumber,
            $"SHB test message — if you can read this, SMS is wired correctly. ({DateTime.UtcNow:HH:mm} UTC)");

        return Ok(new SmsTestResultDto
        {
            Provider = providerType,
            Configured = configured,
            Sent = sent,
            PhoneTo = user.PhoneNumber,
        });
    }
}

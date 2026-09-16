using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

/// One-time dialogs the player has already dealt with.
///
/// Deliberately its own controller rather than a route on SmsPreferencesController: that one carries a
/// class-level [RequireFeature(SmsReminders)], so every route on it 404s whenever SMS reminders are off.
/// A tour flag living there would stop persisting the moment an unrelated feature was toggled.
[ApiVersion("1.0")]
[ApiController]
[Authorize]
public class OnboardingController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(IUserRepository userRepository, ILogger<OnboardingController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/onboarding")]
    [ProducesResponseType(typeof(OnboardingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OnboardingStateDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        return Ok(new OnboardingStateDto { PlayerTourDismissed = user.PlayerTourDismissed });
    }

    /// Idempotent: dismissing an already-dismissed tour is a no-op, not an error.
    [HttpPost("api/v{version:apiVersion}/users/me/onboarding/dismiss-tour")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissTour()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        if (user.PlayerTourDismissed) return NoContent();

        user.PlayerTourDismissed = true;
        await _userRepository.UpdateAsync(user);
        _logger.LogInformation("User {UserId} dismissed the player welcome tour", userId);
        return NoContent();
    }
}

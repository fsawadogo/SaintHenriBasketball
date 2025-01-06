using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SeasonSubscriptionsController : BaseApiController
{
    private readonly ISeasonSubscriptionService _subscriptionService;
    private readonly ILogger<SeasonSubscriptionsController> _logger;

    public SeasonSubscriptionsController(
        ISeasonSubscriptionService subscriptionService,
        ILogger<SeasonSubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new season subscription for current user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonSubscriptionDto>> CreateSubscription([FromBody] CreateSeasonSubscriptionDto createDto)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var result = await _subscriptionService.CreateSubscriptionAsync(Guid.Parse(userId), createDto);
            return CreatedAtAction(nameof(GetSubscription), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get current user's active subscription
    /// </summary>
    [HttpGet("my-subscription")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonSubscriptionDto>> GetMySubscription()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var subscription = await _subscriptionService.GetActiveSubscriptionAsync(Guid.Parse(userId));
            return Ok(subscription);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get all subscriptions (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonSubscriptionDto>>> GetAllSubscriptions()
    {
        var subscriptions = await _subscriptionService.GetAllSubscriptionsAsync();
        return Ok(subscriptions);
    }

    /// <summary>
    /// Get active subscriptions (Admin only)
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonSubscriptionDto>>> GetActiveSubscriptions()
    {
        var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
        return Ok(subscriptions);
    }

    /// <summary>
    /// Cancel a subscription (Admin only)
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        try
        {
            await _subscriptionService.CancelSubscriptionAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get a specific subscription by ID (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonSubscriptionDto>> GetSubscription(Guid id)
    {
        try
        {
            var subscription = await _subscriptionService.GetSubscriptionAsync(id);
            return Ok(subscription);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Update subscription payment status (Admin only)
    /// </summary>
    [HttpPut("{id}/payment-status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] PaymentStatus status)
    {
        try
        {
            await _subscriptionService.UpdatePaymentStatusAsync(id, status);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Check if user has active subscription
    /// </summary>
    [HttpGet("check-active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> HasActiveSubscription()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var hasActive = await _subscriptionService.HasActiveSubscriptionAsync(Guid.Parse(userId));
        return Ok(hasActive);
    }
}
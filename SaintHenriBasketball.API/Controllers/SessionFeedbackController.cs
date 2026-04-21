using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SessionFeedback;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.SessionFeedback)]
public class SessionFeedbackController : BaseApiController
{
    private readonly ISessionFeedbackService _feedbackService;

    public SessionFeedbackController(ISessionFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/pending-feedback")]
    [ProducesResponseType(typeof(PendingFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetPending()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var pending = await _feedbackService.GetPendingForUserAsync(userId.Value);
        if (pending is null) return NoContent();
        return Ok(pending);
    }

    [HttpPost("api/v{version:apiVersion}/sessions/{sessionId:guid}/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(Guid sessionId, [FromBody] SubmitSessionFeedbackDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _feedbackService.SubmitAsync(userId.Value, sessionId, body.Rating, body.Comment);
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/feedback")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SessionFeedbackSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionFeedbackSummaryDto>> GetForSession(Guid sessionId)
    {
        var summary = await _feedbackService.GetForSessionAsync(sessionId);
        return Ok(summary);
    }
}

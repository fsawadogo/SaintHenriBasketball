using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.OutstandingBalances;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// Who owes the club money, reminders for it, and the history of those reminders.
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/balances")]
[Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.OutstandingBalances)]
public class OutstandingBalancesController : BaseApiController
{
    private readonly IOutstandingBalancesService _service;

    public OutstandingBalancesController(IOutstandingBalancesService service)
    {
        _service = service;
    }

    /// <summary>
    /// Unpaid season fees, unpaid drop-ins and Interac transfers awaiting review, with totals per kind
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OutstandingBalancesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OutstandingBalancesDto>> GetBalances(
        [FromQuery] string? kind = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? direction = null)
    {
        try
        {
            return Ok(await _service.GetBalancesAsync(new OutstandingBalancesQuery { Kind = kind, Sort = sort, Direction = direction }));
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Emails payment reminders for the selected balances (or every balance of a kind). Audited with the counts.
    /// </summary>
    [HttpPost("reminders")]
    [ProducesResponseType(typeof(SendRemindersResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendRemindersResultDto>> SendReminders([FromBody] SendRemindersRequestDto body)
    {
        try
        {
            return Ok(await _service.SendRemindersAsync(body, GetUserId(), User.AuditUserName()));
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Reminder history, newest first, optionally for one player
    /// </summary>
    [HttpGet("reminders")]
    [ProducesResponseType(typeof(ReminderLogPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderLogPageDto>> GetReminderHistory(
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            return Ok(await _service.GetReminderHistoryAsync(userId, page, pageSize));
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

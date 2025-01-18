using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class CommunicationController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CommunicationController> _logger;

    public CommunicationController(
        IEmailService emailService,
        ILogger<CommunicationController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Send payment reminders to specified users
    /// </summary>
    [HttpPost("payment-reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendPaymentReminders([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.PaymentReminder,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "payment reminder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment reminders");
            return StatusCode(500, "An error occurred while sending payment reminders");
        }
    }

    /// <summary>
    /// Send attendance reminders to specified users
    /// </summary>
    [HttpPost("attendance-reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendAttendanceReminders([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.AttendanceReminder,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "attendance reminder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending attendance reminders");
            return StatusCode(500, "An error occurred while sending attendance reminders");
        }
    }

    /// <summary>
    /// Send season registration reminders to specified users
    /// </summary>
    [HttpPost("season-registration-reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendSeasonRegistrationReminders([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.SeasonRegistrationReminder,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "season registration reminder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending season registration reminders");
            return StatusCode(500, "An error occurred while sending season registration reminders");
        }
    }

    /// <summary>
    /// Send facility update notifications to specified users
    /// </summary>
    [HttpPost("facility-update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendFacilityUpdates([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.FacilityUpdate,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "facility update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending facility updates");
            return StatusCode(500, "An error occurred while sending facility updates");
        }
    }

    /// <summary>
    /// Send schedule change notifications to specified users
    /// </summary>
    [HttpPost("schedule-change")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendScheduleChanges([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.ScheduleChange,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "schedule change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending schedule changes");
            return StatusCode(500, "An error occurred while sending schedule changes");
        }
    }

    /// <summary>
    /// Send general announcements to specified users
    /// </summary>
    [HttpPost("announcement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendAnnouncements([FromBody] EmailRequestDto request)
    {
        try
        {
            var result = await _emailService.SendTargetedEmailsAsync(
                EmailType.GeneralAnnouncement,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, "announcement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending announcements");
            return StatusCode(500, "An error occurred while sending announcements");
        }
    }

    private IActionResult HandleEmailResult(EmailSendResult result, string emailType)
    {
        if (!result.AllSucceeded)
        {
            return Ok(new
            {
                Message = $"Some {emailType} emails failed to send",
                SuccessCount = result.SuccessCount,
                FailureCount = result.FailureCount,
                FailedEmails = result.FailedEmails
            });
        }

        return Ok(new
        {
            Message = $"Successfully sent {emailType} emails to {result.SuccessCount} recipients",
            SuccessCount = result.SuccessCount
        });
    }
}

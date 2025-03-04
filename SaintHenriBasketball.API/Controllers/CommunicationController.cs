using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class CommunicationController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CommunicationController> _logger;

    /// <inheritdoc />
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
            var result = await _emailService.SendPaymentRemindersAsync(
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
            var result = await _emailService.SendAttendanceRemindersAsync(
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
            var result = await _emailService.SendSeasonRegistrationRemindersAsync(
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
    /// Send general announcements to specified users
    /// </summary>
    [HttpPost("announcement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendAnnouncements([FromBody] EmailRequestDto request)
    {
        if (string.IsNullOrEmpty(request.CustomMessage))
        {
            return BadRequest("Announcement message is required");
        }

        try
        {
            var result = await _emailService.SendGeneralAnnouncementsAsync(
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

    /// <summary>
    /// Send custom email based on email type
    /// </summary>
    [HttpPost("custom")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendCustomEmail([FromBody] CustomEmailRequestDto request)
    {
        try
        {
            if (!request.Emails.Any())
            {
                return BadRequest("At least one email address is required");
            }

            var result = await _emailService.SendTargetedEmailsAsync(
                request.EmailType,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            return HandleEmailResult(result, request.EmailType.ToString().ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {EmailType} emails", request.EmailType);
            return StatusCode(500, $"An error occurred while sending {request.EmailType} emails");
        }
    }
    
    private IActionResult HandleEmailResult(EmailSendResult result, string emailType)
    {
        var response = new EmailSendResponseDto
        {
            Message = result.AllSucceeded 
                ? $"Successfully sent {emailType} emails to {result.SuccessCount} recipients"
                : $"Some {emailType} emails failed to send",
            SuccessCount = result.SuccessCount,
            FailureCount = result.FailureCount,
            FailedEmails = result.FailedEmails
        };

        return Ok(response);
    }
}
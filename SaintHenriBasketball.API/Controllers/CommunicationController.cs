using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Exceptions;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class CommunicationController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IUserService _userService;
    private readonly ISeasonService _seasonService;
    private readonly ILogger<CommunicationController> _logger;

    /// <inheritdoc />
    public CommunicationController(
        IEmailService emailService,
        IUserService userService,
        ISeasonService seasonService,
        ILogger<CommunicationController> logger)
    {
        _emailService = emailService;
        _userService = userService;
        _seasonService = seasonService;
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

    /// <summary>
    /// Send season start announcement to all users
    /// </summary>
    [HttpPost("season-start-announcement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendSeasonStartAnnouncement([FromBody] SeasonStartAnnouncementDto? request = null)
    {
        try
        {
            // Get current season
            var season = await _seasonService.GetCurrentSeasonAsync();
            if (season == null)
            {
                return NotFound("No active season found. Please ensure a season is currently active.");
            }

            // Get all users
            var users = await _userService.GetAllUsersAsync();
            var userEmails = users.Select(u => u.Email).Where(e => !string.IsNullOrEmpty(e)).Cast<string?>().ToList();

            if (!userEmails.Any())
            {
                return BadRequest("No users found to send the announcement to");
            }

            // Create bilingual announcement messages
            var englishMessage = request?.CustomMessage ?? CreateSeasonStartMessageEn(season);
            var frenchMessage = request?.CustomMessageFr ?? CreateSeasonStartMessageFr(season);

            // Send announcement to all users
            var result = await _emailService.SendGeneralAnnouncementsAsync(
                userEmails,
                request?.Language ?? Domain.Enums.EmailLanguage.English,
                englishMessage,
                frenchMessage);

            _logger.LogInformation(
                "Season start announcement sent. Success: {SuccessCount}, Failed: {FailureCount}",
                result.SuccessCount,
                result.FailureCount);

            return HandleEmailResult(result, "season start announcement");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Season not found for start announcement");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending season start announcement");
            return StatusCode(500, "An error occurred while sending season start announcement");
        }
    }

    private string CreateSeasonStartMessageEn(SeasonDto season)
    {
        return $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <h2 style=""color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px;"">
        🏀 New Season Starting!
    </h2>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        Dear Basketball Enthusiast,
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        We are excited to announce that a new season is starting!
    </p>
    
    <div style=""background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0;"">
        <h3 style=""color: #2c3e50; margin-top: 0;"">Season Details:</h3>
        <ul style=""color: #34495e; line-height: 1.8;"">
            <li><strong>Start Date:</strong> {season.StartDate:MMMM dd, yyyy}</li>
            <li><strong>End Date:</strong> {season.EndDate:MMMM dd, yyyy}</li>
            <li><strong>Price:</strong> ${season.Price:F2}</li>
        </ul>
    </div>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        We look forward to seeing you on the court! Make sure to register for sessions and stay active throughout the season.
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        If you have any questions, please don't hesitate to contact us.
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e; margin-top: 30px;"">
        Best regards,<br>
        <strong>Saint Henri Basketball Team</strong>
    </p>
</div>";
    }

    private string CreateSeasonStartMessageFr(SeasonDto season)
    {
        return $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <h2 style=""color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px;"">
        🏀 Nouvelle Saison qui Commence!
    </h2>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        Cher Enthousiaste du Basketball,
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        Nous sommes ravis d'annoncer qu'une nouvelle saison commence!
    </p>
    
    <div style=""background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0;"">
        <h3 style=""color: #2c3e50; margin-top: 0;"">Détails de la Saison:</h3>
        <ul style=""color: #34495e; line-height: 1.8;"">
            <li><strong>Date de Début:</strong> {season.StartDate:dd MMMM yyyy}</li>
            <li><strong>Date de Fin:</strong> {season.EndDate:dd MMMM yyyy}</li>
            <li><strong>Prix:</strong> {season.Price:F2} $</li>
        </ul>
    </div>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        Nous avons hâte de vous voir sur le terrain! Assurez-vous de vous inscrire aux séances et de rester actif tout au long de la saison.
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">
        Si vous avez des questions, n'hésitez pas à nous contacter.
    </p>
    
    <p style=""font-size: 16px; line-height: 1.6; color: #34495e; margin-top: 30px;"">
        Cordialement,<br>
        <strong>L'Équipe de Basketball Saint Henri</strong>
    </p>
</div>";
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
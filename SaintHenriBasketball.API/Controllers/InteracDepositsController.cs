using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.InteracDeposits;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// Interac auto-deposit confirmations forwarded from the club's mailbox, and what they paid for.
/// Hidden (404) while the interac-auto-match flag is off.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/payments/interac-deposits")]
[Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.InteracAutoMatch)]
public class InteracDepositsController : ControllerBase
{
    private readonly IInteracDepositService _service;

    public InteracDepositsController(IInteracDepositService service)
    {
        _service = service;
    }

    private Guid? AdminId => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private string AdminName => User.Identity?.Name ?? "Admin";

    /// <summary>Deposits newest first, each with the payment it suggests when nothing matched outright.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InteracDepositDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InteracDepositDto>>> List([FromQuery] bool unmatchedOnly = false) =>
        Ok(await _service.ListAsync(unmatchedOnly));

    /// <summary>Ties a deposit to a pending payment and marks that payment paid.</summary>
    [HttpPost("{depositId:guid}/match")]
    [ProducesResponseType(typeof(InteracDepositDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InteracDepositDto>> Match(Guid depositId, [FromBody] MatchInteracDepositDto body)
    {
        try { return Ok(await _service.MatchAsync(depositId, body.PaymentId, AdminId, AdminName)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Sets a deposit aside: it is not a session payment.</summary>
    [HttpPost("{depositId:guid}/ignore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ignore(Guid depositId, [FromBody] IgnoreInteracDepositDto body)
    {
        try { await _service.IgnoreAsync(depositId, body.Note, AdminId, AdminName); return NoContent(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>An admin pastes a confirmation email by hand, when forwarding is not set up.</summary>
    [HttpPost("paste")]
    [ProducesResponseType(typeof(IngestInteracEmailResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestInteracEmailResultDto>> Paste([FromBody] IngestInteracEmailDto body) =>
        Ok(await _service.IngestAsync(body));
}

/// <summary>
/// Where the club's mailbox forwards Interac confirmations. Not a browser endpoint: a forwarding
/// service posts here, so it proves itself with a shared token, an optional body signature, and an
/// email that really came from Interac.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/webhooks/interac-deposits")]
[AllowAnonymous]
[RequireFeature(FeatureFlagKeys.InteracAutoMatch)]
public class InteracWebhookController : ControllerBase
{
    public const string TokenHeader = InteracWebhookVerification.TokenHeader;
    public const string SignatureHeader = InteracWebhookVerification.SignatureHeader;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IInteracDepositService _service;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InteracWebhookController> _logger;

    public InteracWebhookController(IInteracDepositService service, IConfiguration configuration, ILogger<InteracWebhookController> logger)
    {
        _service = service;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Takes one forwarded confirmation.
    /// </summary>
    /// <remarks>
    /// Answers 200 for every email it understands, including one already seen and one that is not a
    /// deposit at all, so a forwarding service never retries an email it can never deliver. Only a
    /// genuine failure answers 5xx, which is the case worth retrying.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(IngestInteracEmailResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IngestInteracEmailResultDto>> Receive()
    {
        // The signature covers the bytes as sent, so the body is read raw rather than model-bound.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        var token = _configuration["Payments:InteracWebhookToken"];
        // With no token configured the door stays shut, rather than standing open.
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Interac webhook called while no token is configured");
            return Unauthorized();
        }

        if (!InteracWebhookVerification.SecretMatches(Request.Headers[TokenHeader].ToString(), token))
        {
            _logger.LogWarning("Interac webhook called with a wrong token");
            return Unauthorized();
        }

        // A signing secret is optional, but once set every call must carry a matching signature.
        var signingSecret = _configuration["Payments:InteracWebhookSigningSecret"];
        if (!string.IsNullOrWhiteSpace(signingSecret)
            && !InteracWebhookVerification.SignatureMatches(Request.Headers[SignatureHeader].ToString(), rawBody, signingSecret))
        {
            _logger.LogWarning("Interac webhook called with a wrong body signature");
            return Unauthorized();
        }

        IngestInteracEmailDto? body;
        try
        {
            body = JsonSerializer.Deserialize<IngestInteracEmailDto>(rawBody, Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Interac webhook called with a body that is not JSON");
            return BadRequest("The body must be JSON.");
        }
        if (body == null) return BadRequest("The body must be JSON.");

        // A token can leak. The email must also look like one Interac sent, so a leaked token alone
        // cannot mark money as received.
        if (_configuration.GetValue("Payments:InteracRequireSender", true))
        {
            var sender = InteracWebhookVerification.OriginalSender(body.From, body.Body);
            var allowed = InteracWebhookVerification.ParseDomains(_configuration["Payments:InteracSenderAllowlist"]);
            if (!InteracWebhookVerification.SenderAllowed(sender, allowed))
            {
                _logger.LogWarning("Interac webhook refused an email from {Sender}", sender ?? "an unknown sender");
                return BadRequest("The email does not come from Interac.");
            }
        }

        return Ok(await _service.IngestAsync(body));
    }
}

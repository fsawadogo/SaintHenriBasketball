using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
/// service posts here with a shared token, because it cannot hold a person's sign-in.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/webhooks/interac-deposits")]
[AllowAnonymous]
[RequireFeature(FeatureFlagKeys.InteracAutoMatch)]
public class InteracWebhookController : ControllerBase
{
    public const string TokenHeader = "X-SHB-Webhook-Token";

    private readonly IInteracDepositService _service;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InteracWebhookController> _logger;

    public InteracWebhookController(IInteracDepositService service, IConfiguration configuration, ILogger<InteracWebhookController> logger)
    {
        _service = service;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(IngestInteracEmailResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IngestInteracEmailResultDto>> Receive([FromBody] IngestInteracEmailDto body)
    {
        var expected = _configuration["Payments:InteracWebhookToken"];
        // With no token configured the door stays shut, rather than standing open.
        if (string.IsNullOrWhiteSpace(expected)) return Unauthorized();

        var provided = Request.Headers[TokenHeader].ToString();
        if (!FixedTimeEquals(provided, expected))
        {
            _logger.LogWarning("Interac webhook called with a wrong token");
            return Unauthorized();
        }

        return Ok(await _service.IngestAsync(body));
    }

    /// Constant-time compare, so a wrong token cannot be guessed one character at a time.
    private static bool FixedTimeEquals(string? provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}

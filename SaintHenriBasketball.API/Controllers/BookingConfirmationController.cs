using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// Sending booking confirmations that never went out — places reserved before the email existed, or
/// whose send failed at the time.
///
/// Two steps, because it reaches real players: a preview that writes nothing and names everyone, then
/// the send. Each registration is stamped once its email goes out, so a second run reaches nobody twice.
/// Hidden (404) while the booking-confirmation-email flag is off.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/bookings/confirmations")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.BookingConfirmationEmail)]
public class BookingConfirmationController : ControllerBase
{
    private readonly IBookingConfirmationBackfillService _backfill;

    public BookingConfirmationController(IBookingConfirmationBackfillService backfill)
    {
        _backfill = backfill;
    }

    /// <summary>Who is still owed a confirmation. Sends nothing.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(BookingConfirmationBackfillResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingConfirmationBackfillResultDto>> Pending([FromQuery] Guid? sessionId) =>
        Ok(await _backfill.RunAsync(sessionId, dryRun: true));

    /// <summary>Sends the missing confirmations. Players who already had one are skipped.</summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(BookingConfirmationBackfillResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingConfirmationBackfillResultDto>> Send([FromQuery] Guid? sessionId) =>
        Ok(await _backfill.RunAsync(sessionId));
}

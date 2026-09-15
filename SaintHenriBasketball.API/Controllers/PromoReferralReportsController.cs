using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.PromoReferralReports;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// What promos, referrals and account credits cost the club, and admin control over referral codes.
[ApiVersion("1.0")]
[ApiController]
public class PromoReferralReportsController : BaseApiController
{
    private readonly IPromoReferralReportService _reports;
    private readonly IReferralCodeAdminService _referralCodes;

    public PromoReferralReportsController(IPromoReferralReportService reports, IReferralCodeAdminService referralCodes)
    {
        _reports = reports;
        _referralCodes = referralCodes;
    }

    [HttpGet("api/v{version:apiVersion}/admin/reports/promos-referrals/promos")]
    [Authorize(Roles = "Admin")]
    [RequireFeature(FeatureFlagKeys.PromoReferralReports)]
    [ProducesResponseType(typeof(PromoUsageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromoUsageReportDto>> GetPromoUsage([FromQuery] ReportRangeQuery range)
    {
        try { return Ok(await _reports.GetPromoUsageAsync(range)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/reports/promos-referrals/credits")]
    [Authorize(Roles = "Admin")]
    [RequireFeature(FeatureFlagKeys.PromoReferralReports)]
    [ProducesResponseType(typeof(CreditsReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreditsReportDto>> GetCredits([FromQuery] ReportRangeQuery range)
    {
        try { return Ok(await _reports.GetCreditsReportAsync(range)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/referral-codes")]
    [Authorize(Roles = "Admin")]
    [RequireFeature(FeatureFlagKeys.PromoReferralReports)]
    [ProducesResponseType(typeof(ReferralCodeAdminPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferralCodeAdminPageDto>> GetReferralCodes([FromQuery] ReferralCodeAdminQuery query) =>
        Ok(await _referralCodes.SearchAsync(query));

    [HttpPut("api/v{version:apiVersion}/admin/referral-codes/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [RequireFeature(FeatureFlagKeys.PromoReferralReports)]
    [ProducesResponseType(typeof(ReferralCodeAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReferralCodeAdminDto>> UpdateReferralCode(Guid id, [FromBody] UpdateReferralCodeDto body)
    {
        try { return Ok(await _referralCodes.UpdateAsync(id, body, User.AuditUserId(), User.AuditUserName())); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// Resetting a season's plan choices.
///
/// This is a mass mutation over every player in a season, so it is deliberately two steps: a
/// preview that writes nothing and shows the blast radius, then the reset itself.
///
/// Paid passes are never cleared. That is the safety contract and it lives in the service.
[ApiVersion("1.0")]
[ApiController]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.SeasonPlanChoice)]
public class AdminSeasonPlanController : BaseApiController
{
    private readonly ISeasonPlanService _seasonPlan;

    public AdminSeasonPlanController(ISeasonPlanService seasonPlan)
    {
        _seasonPlan = seasonPlan;
    }

    /// What a reset would do. Changes nothing.
    [HttpGet("api/v{version:apiVersion}/admin/seasons/{seasonId:guid}/plan-choices/reset/preview")]
    [ProducesResponseType(typeof(SeasonPlanResetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonPlanResetDto>> PreviewReset(Guid seasonId) =>
        Ok(await _seasonPlan.PreviewResetAsync(seasonId));

    /// Clears the season's unpaid choices and puts those players back on drop-in.
    ///
    /// [SkipAdminAudit] because the service writes its own richer entry with the counts;
    /// AdminMutationAuditFilter would otherwise dedupe or duplicate it.
    [HttpPost("api/v{version:apiVersion}/admin/seasons/{seasonId:guid}/plan-choices/reset")]
    [SkipAdminAudit]
    [ProducesResponseType(typeof(SeasonPlanResetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SeasonPlanResetDto>> Reset(Guid seasonId)
    {
        var adminId = GetUserId();
        if (adminId is null) return Unauthorized();

        return Ok(await _seasonPlan.ResetAsync(seasonId, adminId.Value, User.AuditUserName()));
    }
}

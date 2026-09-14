using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Reconciliation;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/payments/reconciliation")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.InteracReconciliation)]
public class ReconciliationController : BaseApiController
{
    private readonly IReconciliationService _reconciliationService;

    public ReconciliationController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PendingPaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingPaymentDto>>> GetPending()
    {
        var pending = await _reconciliationService.GetPendingAsync();
        return Ok(pending);
    }

    [HttpPost("bulk-complete")]
    [ProducesResponseType(typeof(BulkCompletePaymentsResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkCompletePaymentsResultDto>> BulkComplete([FromBody] BulkCompletePaymentsDto body)
    {
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
        var result = await _reconciliationService.BulkCompleteAsync(body.PaymentIds, GetUserId(), adminName);
        return Ok(result);
    }
}

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
    public const int MaxBulkPayments = 200;

    private readonly IReconciliationService _reconciliationService;
    private readonly ICacheService _cacheService;

    public ReconciliationController(IReconciliationService reconciliationService, ICacheService cacheService)
    {
        _reconciliationService = reconciliationService;
        _cacheService = cacheService;
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
        if (body.PaymentIds.Count > MaxBulkPayments) return BadRequest($"Confirm at most {MaxBulkPayments} transfers at a time.");
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
        var result = await _reconciliationService.BulkCompleteAsync(body.PaymentIds, GetUserId(), adminName);
        await InvalidatePaymentListsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Transfers the club couldn't find in the bank: marked failed, player emailed, credit returned. Audited.
    /// </summary>
    [HttpPost("mark-not-received")]
    [ProducesResponseType(typeof(BulkMarkNotReceivedResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkMarkNotReceivedResultDto>> MarkNotReceived([FromBody] BulkMarkNotReceivedDto body)
    {
        if (body.PaymentIds.Count > MaxBulkPayments) return BadRequest($"Update at most {MaxBulkPayments} transfers at a time.");
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
        try
        {
            var result = await _reconciliationService.BulkMarkNotReceivedAsync(body.PaymentIds, body.Note, GetUserId(), adminName);
            await InvalidatePaymentListsAsync();
            return Ok(result);
        }
        catch (SaintHenriBasketball.Application.Exceptions.ValidationException ex) { return BadRequest(ex.Message); }
    }

    // Payment lists are cached for minutes; a reconciliation must show up right away.
    private async Task InvalidatePaymentListsAsync()
    {
        await _cacheService.RemoveAsync("Payments:All");
        await _cacheService.RemoveAsync("Payments:Pending");
        await _cacheService.RemoveAsync("Payments:Summary");
        // Each player's payment history and each payment's details are cached separately.
        await _cacheService.RemoveByPrefixAsync("Payments:User");
        await _cacheService.RemoveByPrefixAsync("Payments:Detail");
    }
}

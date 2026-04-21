using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.TaxReceipts;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.TaxReceipts)]
public class TaxReceiptsController : BaseApiController
{
    private readonly ITaxReceiptService _taxReceiptService;

    public TaxReceiptsController(ITaxReceiptService taxReceiptService)
    {
        _taxReceiptService = taxReceiptService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/tax-receipts")]
    [ProducesResponseType(typeof(IReadOnlyList<TaxReceiptYearDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaxReceiptYearDto>>> GetAvailableYears()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var years = await _taxReceiptService.GetAvailableYearsAsync(userId.Value);
        return Ok(years);
    }

    [HttpGet("api/v{version:apiVersion}/users/me/tax-receipts/{year:int}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(int year)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var (pdf, fileName) = await _taxReceiptService.GenerateAsync(userId.Value, year);
            return File(pdf, "application/pdf", fileName);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

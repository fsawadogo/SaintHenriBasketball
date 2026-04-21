using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.PromoCodes;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[RequireFeature(FeatureFlagKeys.PromoCodes)]
public class PromoCodesController : BaseApiController
{
    private readonly IPromoCodeService _promoCodeService;

    public PromoCodesController(IPromoCodeService promoCodeService)
    {
        _promoCodeService = promoCodeService;
    }

    [HttpGet("api/v{version:apiVersion}/admin/promo-codes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PromoCodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromoCodeDto>>> GetAll()
    {
        var codes = await _promoCodeService.GetAllAsync();
        return Ok(codes);
    }

    [HttpPost("api/v{version:apiVersion}/admin/promo-codes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PromoCodeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromoCodeDto>> Create([FromBody] UpsertPromoCodeDto body)
    {
        try
        {
            var code = await _promoCodeService.CreateAsync(body);
            return CreatedAtAction(nameof(GetAll), new { version = "1.0" }, code);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("api/v{version:apiVersion}/admin/promo-codes/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PromoCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoCodeDto>> Update(Guid id, [FromBody] UpsertPromoCodeDto body)
    {
        try
        {
            var code = await _promoCodeService.UpdateAsync(id, body);
            return Ok(code);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("api/v{version:apiVersion}/admin/promo-codes/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _promoCodeService.DeleteAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("api/v{version:apiVersion}/promo-codes/validate")]
    [Authorize]
    [ProducesResponseType(typeof(ValidatePromoCodeResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidatePromoCodeResultDto>> Validate([FromBody] ValidatePromoCodeDto body)
    {
        var result = await _promoCodeService.ValidateAsync(body);
        return Ok(result);
    }
}

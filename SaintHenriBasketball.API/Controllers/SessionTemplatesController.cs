using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SessionTemplate;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/session-templates")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.RecurringSessions)]
public class SessionTemplatesController : BaseApiController
{
    private readonly ISessionTemplateService _templateService;

    public SessionTemplatesController(ISessionTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SessionTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionTemplateDto>>> GetAll()
    {
        var templates = await _templateService.GetAllAsync();
        return Ok(templates);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SessionTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionTemplateDto>> Create([FromBody] UpsertSessionTemplateDto body)
    {
        try
        {
            var created = await _templateService.CreateAsync(body);
            return CreatedAtAction(nameof(GetAll), new { version = "1.0" }, created);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SessionTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionTemplateDto>> Update(Guid id, [FromBody] UpsertSessionTemplateDto body)
    {
        try
        {
            var updated = await _templateService.UpdateAsync(id, body);
            return Ok(updated);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _templateService.DeleteAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:guid}/generate")]
    [ProducesResponseType(typeof(GenerateSessionsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateSessionsResultDto>> Generate(Guid id, [FromBody] GenerateSessionsRequestDto body)
    {
        try
        {
            var result = await _templateService.GenerateSessionsAsync(id, body.StartDate, body.EndDate);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}

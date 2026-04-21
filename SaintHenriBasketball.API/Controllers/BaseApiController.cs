using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

// Derived controllers declare their own class-level [Route]; intentionally no
// default here so method-level [HttpGet("api/...")] templates aren't prefixed.
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected Guid? GetUserId()
    {
        var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
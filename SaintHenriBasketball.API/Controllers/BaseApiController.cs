using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected Guid? GetUserId()
    {
        var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
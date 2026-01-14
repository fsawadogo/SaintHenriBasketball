using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class CacheController : Controller
{
    [HttpGet("metrics")]
    public IActionResult GetCacheMetrics([FromServices] ICacheService cacheService)
    {
        if (cacheService is MemoryCacheService memoryCacheService)
        {
            return Ok(new
            {
                memoryCacheService.Hits,
                memoryCacheService.Misses,
                HitRatio = memoryCacheService.HitRatio.ToString("P2") // Percentage format
            });
        }

        return BadRequest("Cache metrics not available");
    }

    [HttpGet("cache-stats")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetCacheStats([FromServices] IMemoryCache memoryCache)
    {
        // This is a simple implementation - for more detailed stats,
        // you would need to add hit/miss counters to your MemoryCacheService
        return Ok(new
        {
            Message = "Cache is active",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Clear all season-related cache entries (Admin only)
    /// </summary>
    [HttpDelete("seasons")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearSeasonsCache([FromServices] ICacheService cacheService)
    {
        await cacheService.RemoveAsync("CurrentSeason");
        await cacheService.RemoveAsync("AllSeasons");
        await cacheService.RemoveByPrefixAsync("Season_");
        
        return Ok(new
        {
            Message = "Season cache cleared successfully",
            Timestamp = DateTime.UtcNow
        });
    }
}

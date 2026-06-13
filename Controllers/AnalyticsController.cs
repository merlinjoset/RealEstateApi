using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    /// <summary>
    /// Record an anonymous page view. Called by the SPA on every route change,
    /// so it's public and intentionally cheap (fire-and-forget on the client).
    /// </summary>
    [HttpPost("pageview")]
    [AllowAnonymous]
    public async Task<IActionResult> Track([FromBody] TrackPageViewRequest req)
    {
        await analytics.RecordAsync(req);
        return NoContent();
    }

    /// <summary>Aggregated traffic summary for the admin Traffic dashboard.</summary>
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AnalyticsSummaryDto>> Summary([FromQuery] int days = 30)
    {
        if (days is < 1 or > 365) days = 30;
        return Ok(await analytics.GetSummaryAsync(days));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISiteSettingService settings) : ControllerBase
{
    /// <summary>
    /// Read site-wide settings (social URLs etc.). Public — the Footer of
    /// every page calls this on load, so anonymous access is required.
    /// </summary>
    [HttpGet("site")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteSettingDto>> GetSite() =>
        Ok(await settings.GetAsync());

    /// <summary>Update site-wide settings. Admin only.</summary>
    [HttpPut("site")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SiteSettingDto>> UpdateSite([FromBody] UpdateSiteSettingRequest req) =>
        Ok(await settings.UpdateAsync(req));
}

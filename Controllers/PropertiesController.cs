using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/properties")]
public class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    private int? CurrentUserId =>
        User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;

    private bool IsAdmin =>
        User.FindFirstValue(ClaimTypes.Role) == "Admin";

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PropertyQueryParams q)
    {
        var result = await propertyService.GetAllAsync(q);
        return Ok(result);
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured()
    {
        var result = await propertyService.GetFeaturedAsync();
        return Ok(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending()
    {
        var result = await propertyService.GetPendingAsync();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await propertyService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}/related")]
    public async Task<IActionResult> GetRelated(int id)
    {
        var result = await propertyService.GetRelatedAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest req)
    {
        var autoApprove = IsAdmin;
        var result = await propertyService.CreateAsync(req, CurrentUserId, autoApprove);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyRequest req)
    {
        var result = await propertyService.UpdateAsync(id, req);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await propertyService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveOrReject(int id, [FromBody] ApprovalRequest req)
    {
        if (req.Action.ToLower() is not ("approve" or "reject"))
            return BadRequest(new { message = "Action must be 'approve' or 'reject'" });

        var result = await propertyService.ApproveOrRejectAsync(id, req);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:int}/favorite")]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var userId = CurrentUserId!.Value;
        var saved = await propertyService.ToggleFavoriteAsync(id, userId);
        return Ok(new { saved });
    }
}

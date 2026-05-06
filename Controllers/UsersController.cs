using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/users")]
// [Authorize(Roles = "Admin")] — TODO: wire up role-based auth; left open for dev
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserListResponse>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status)
    {
        var result = await users.GetAllAsync(new UserQueryParams(search, role, status));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminUserDto>> GetById(int id)
    {
        var u = await users.GetByIdAsync(id);
        return u is null ? NotFound() : Ok(u);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateUserRequest req)
    {
        try
        {
            var created = await users.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex.Message.Contains("unique") || ex.InnerException?.Message.Contains("unique") == true)
        {
            return Conflict(new { message = "Email already registered" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminUserDto>> Update(int id, [FromBody] UpdateUserRequest req)
    {
        try
        {
            var updated = await users.UpdateAsync(id, req);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<AdminUserDto>> ToggleStatus(int id)
    {
        var u = await users.ToggleStatusAsync(id);
        return u is null ? NotFound() : Ok(u);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await users.DeleteAsync(id);
        if (!ok) return BadRequest(new { message = "User not found or cannot be deleted (admin accounts protected)." });
        return NoContent();
    }
}

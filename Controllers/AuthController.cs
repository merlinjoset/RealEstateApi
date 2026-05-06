using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await authService.LoginAsync(req);
        if (result is null) return Unauthorized(new { message = "Invalid email or password" });
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var result = await authService.RegisterAsync(req);
            return Created("", result);
        }
        catch (Exception ex) when (ex.Message.Contains("unique") || ex.InnerException?.Message.Contains("unique") == true)
        {
            return Conflict(new { message = "Email already registered" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var result = await authService.RefreshAsync(req.RefreshToken);
        if (result is null) return Unauthorized(new { message = "Invalid or expired refresh token" });
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await authService.RevokeAsync(req.RefreshToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await authService.GetMeAsync(userId);
        if (user is null) return NotFound();
        return Ok(user);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var updated = await authService.UpdateProfileAsync(userId, req);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (Exception ex) when (ex.Message.Contains("unique") || ex.InnerException?.Message.Contains("unique") == true)
        {
            return Conflict(new { message = "Email already in use" });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await authService.ChangePasswordAsync(userId, req);
        if (!ok) return BadRequest(new { message = "Current password is incorrect" });
        return NoContent();
    }
}

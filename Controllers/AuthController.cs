using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IRsaKeyService rsa) : ControllerBase
{
    /// <summary>
    /// Public RSA key (JWK format) the browser uses to encrypt the password
    /// before sending. Safe to expose — only the matching private key on the
    /// server can decrypt.
    /// </summary>
    [HttpGet("public-key")]
    public IActionResult GetPublicKey() => Ok(rsa.GetPublicJwk());

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Prefer encrypted password — decrypt with private key.
        // Fall back to plaintext password for backwards compatibility (curl/tests).
        string? plaintext = req.Password;
        if (!string.IsNullOrWhiteSpace(req.EncryptedPassword))
        {
            try
            {
                plaintext = rsa.Decrypt(req.EncryptedPassword);
            }
            catch
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }
        }

        if (string.IsNullOrWhiteSpace(plaintext))
            return BadRequest(new { message = "Password is required" });

        var result = await authService.LoginAsync(new LoginRequest(req.Email, plaintext, null));
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

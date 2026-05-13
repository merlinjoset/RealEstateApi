using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest req);
    Task<AuthResponse> RegisterAsync(RegisterRequest req);
    Task<AuthResponse?> RefreshAsync(string refreshToken);
    Task RevokeAsync(string refreshToken);
    Task<UserDto?> GetMeAsync(int userId);
    Task<UserDto?> UpdateProfileAsync(int userId, UpdateProfileRequest req);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest req);
    Task RequestPasswordResetAsync(ForgotPasswordRequest req);
    Task<bool> ResetPasswordWithOtpAsync(ResetPasswordRequest req);
}

public class AuthService(
    AppDbContext db,
    ITokenService tokenService,
    IConfiguration config,
    INotificationService notifications,
    IEmailService email,
    ISmsTemplateService templates,
    ILogger<AuthService> log) : IAuthService
{
    private static UserDto ToDto(User u) => new(
        u.Id, u.FirstName, u.LastName, u.Email,
        u.Phone, u.Role.ToString(), u.Avatar, u.CreatedAt
    );

    private TokensDto BuildTokens(User user)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshDays = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");

        db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
        });

        return new TokensDto(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(
            double.Parse(config["Jwt:ExpiryMinutes"] ?? "60")));
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || string.IsNullOrEmpty(req.Password)
            || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return null;

        var tokens = BuildTokens(user);
        await db.SaveChangesAsync();
        return new AuthResponse(ToDto(user), tokens);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        // Public registration only allows Agent or Seller roles —
        // Employee/Admin accounts are created by the admin team via /api/users.
        var role = (req.Role?.ToLowerInvariant()) switch
        {
            "agent"  => UserRole.Agent,
            "seller" => UserRole.Seller,
            "buyer"  => UserRole.Buyer,
            _        => UserRole.Seller,
        };

        var user = new User
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Phone = req.Phone,
            Role = role,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tokens = BuildTokens(user);
        await db.SaveChangesAsync();
        return new AuthResponse(ToDto(user), tokens);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var rt = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow);

        if (rt is null) return null;

        rt.IsRevoked = true;
        var tokens = BuildTokens(rt.User);
        await db.SaveChangesAsync();
        return new AuthResponse(ToDto(rt.User), tokens);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
        if (rt is null) return;
        rt.IsRevoked = true;
        await db.SaveChangesAsync();
    }

    public async Task<UserDto?> GetMeAsync(int userId)
    {
        var user = await db.Users.FindAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> UpdateProfileAsync(int userId, UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return null;

        user.FirstName = req.FirstName;
        user.LastName = req.LastName;
        user.Email = req.Email;
        user.Phone = req.Phone;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return false;
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash)) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Issue a 6-digit OTP for password reset and deliver it via SMS + email.
    /// Returns silently even if the email isn't on file — keeps the endpoint
    /// from being an account-enumeration oracle.
    /// </summary>
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null)
        {
            // No-op for unknown emails — see comment above. Log so admins
            // can still see attempts in Render.
            log.LogInformation("🔑 [forgot-password] unknown email {Email}", req.Email);
            return;
        }

        // 6-digit zero-padded OTP. Cryptographically random (Random.Shared
        // is not seeded predictably and good enough for short-lived OTPs).
        var otp = Random.Shared.Next(0, 1_000_000).ToString("D6");

        user.PasswordResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
        user.PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(15);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Render the SMS template if it exists; otherwise fall back to a
        // hard-coded body so the reset is never blocked by a missing row.
        var body = await templates.RenderAsync("password.reset.otp", new Dictionary<string, string?>
        {
            ["name"]    = user.FirstName,
            ["otp"]     = otp,
            ["minutes"] = "15",
        }) ?? $"Jose For Land: your password reset code is {otp}. Valid for 15 minutes. Do not share.";

        // Best-effort SMS — WhatsApp first for admins, plain SMS otherwise.
        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            await notifications.SendAsync(user.Phone!, body, preferWhatsApp: true);
        }

        // Email is the more reliable channel (works regardless of DND).
        var html =
            $"<p>Hi {user.FirstName},</p>" +
            $"<p>Your Jose For Land password reset code is:</p>" +
            $"<p style=\"font-size:24px;font-weight:bold;letter-spacing:6px;\">{otp}</p>" +
            $"<p>This code is valid for <strong>15 minutes</strong>. " +
            "If you didn't request a reset, ignore this email — your password stays the same.</p>" +
            "<p>— The Jose For Land team</p>";
        await email.SendAsync(user.Email, "Your password reset code", html);

        log.LogInformation("🔑 [forgot-password] OTP issued for {Email} (expires {Expiry})",
            user.Email, user.PasswordResetExpiresAt);
    }

    /// <summary>
    /// Verify the OTP issued by RequestPasswordResetAsync and rotate the
    /// password hash. Single-use — the OTP is wiped on success. Returns
    /// false for unknown email, expired/missing OTP, or wrong code.
    /// </summary>
    public async Task<bool> ResetPasswordWithOtpAsync(ResetPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null
            || string.IsNullOrWhiteSpace(user.PasswordResetOtpHash)
            || user.PasswordResetExpiresAt is null
            || user.PasswordResetExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Otp, user.PasswordResetOtpHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.PasswordResetOtpHash = null;
        user.PasswordResetExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        log.LogInformation("🔑 [reset-password] password rotated for {Email}", user.Email);
        return true;
    }
}

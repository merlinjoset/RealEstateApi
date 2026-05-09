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
}

public class AuthService(AppDbContext db, ITokenService tokenService, IConfiguration config) : IAuthService
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
}

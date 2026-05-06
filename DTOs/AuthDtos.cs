using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record RegisterRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? Phone,
    string? Role  // "Agent" | "Seller" — defaults to Seller if missing/invalid
);

public record AuthResponse(UserDto User, TokensDto Tokens);

public record TokensDto(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public record RefreshRequest([Required] string RefreshToken);

public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string? Avatar,
    DateTime CreatedAt
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

public record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress] string Email,
    string? Phone
);

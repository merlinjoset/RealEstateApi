using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

/// <summary>
/// Login payload. Either provide <see cref="Password"/> (plaintext over HTTPS)
/// or <see cref="EncryptedPassword"/> (RSA-OAEP-SHA256 ciphertext, base64).
/// The browser client fetches the public key from <c>/api/auth/public-key</c>
/// and encrypts the password before sending so DevTools never shows it in
/// plaintext.
/// </summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    string? Password,
    string? EncryptedPassword
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

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6), MaxLength(6)] string Otp,
    [Required, MinLength(8)] string NewPassword
);

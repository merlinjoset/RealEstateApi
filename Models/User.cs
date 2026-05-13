using System.Text.Json.Serialization;

namespace RealEstateApi.Models;

public enum UserRole { Employee, Seller, Agent, Admin, Buyer }

public class User : ISoftDeletable
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash of the user's password. Never serialised to API clients —
    /// JsonIgnore guarantees this field can't accidentally leak through any
    /// future endpoint that returns a raw User entity instead of a DTO.
    /// </summary>
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public UserRole Role { get; set; } = UserRole.Employee;
    public string? Avatar { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// BCrypt hash of a 6-digit OTP issued by the forgot-password flow. Set
    /// when /auth/forgot-password is called, cleared once /auth/reset-password
    /// successfully consumes it (or when it expires).
    /// </summary>
    [JsonIgnore]
    public string? PasswordResetOtpHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<SavedProperty> SavedProperties { get; set; } = new List<SavedProperty>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
}

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}

namespace RealEstateApi.Models;

public enum UserRole { Employee, Seller, Agent, Admin }

public class User : ISoftDeletable
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public UserRole Role { get; set; } = UserRole.Employee;
    public string? Avatar { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
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

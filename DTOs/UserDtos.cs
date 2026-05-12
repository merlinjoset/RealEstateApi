using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

/// <summary>
/// Admin-facing user record with computed activity stats.
/// </summary>
public record AdminUserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? City,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastActiveAt,
    int PropertiesCount,
    int InquiriesCount
);

public record UserListResponse(
    IReadOnlyList<AdminUserDto> Items,
    int Total,
    UserCountsDto Counts
);

public record UserCountsDto(
    int All,
    int Employee,
    int Seller,
    int Agent,
    int Admin,
    int Buyer,
    int Active,
    int Inactive
);

public record CreateUserRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress] string Email,
    [Required] string Phone,
    string? City,
    [Required] string Role,    // "Employee" | "Seller" | "Agent" | "Admin"
    string? Password           // Optional — defaults to a generated value
);

public record UpdateUserRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress] string Email,
    [Required] string Phone,
    string? City,
    [Required] string Role,
    bool IsActive
);

public record UserQueryParams(
    string? Search = null,
    string? Role = null,        // "all" | "buyer" | "seller" | "agent" | "admin"
    string? Status = null       // "all" | "active" | "inactive"
);

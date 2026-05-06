using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record CreateInquiryRequest(
    [Required, MaxLength(100)] string Name,
    [Required, Phone] string Phone,
    [EmailAddress] string? Email,
    [Required] string Message,
    string PreferredContact,
    int? PropertyId,
    string? Type = null  // "General" | "DocumentRequest" | "SiteVisit" | "Pricing" | "Sell"
);

public record InquiryDto(
    int Id,
    string Name,
    string Phone,
    string? Email,
    string Message,
    string PreferredContact,
    string Type,
    bool IsRead,
    string Status,
    string? Notes,
    int? PropertyId,
    string? PropertyTitle,
    int? AssignedToUserId,
    string? AssignedToName,
    DateTime? AssignedAt,
    DateTime? LastUpdatedAt,
    DateTime CreatedAt
);

public record AssignInquiryRequest(
    [Required] int AssignedToUserId
);

public record UpdateInquiryRequest(
    [Required] string Status,           // "New" | "Assigned" | "InProgress" | "Resolved" | "Closed"
    string? Notes
);

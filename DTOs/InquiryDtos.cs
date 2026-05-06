using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record CreateInquiryRequest(
    [Required, MaxLength(100)] string Name,
    [Required, Phone] string Phone,
    [EmailAddress] string? Email,
    [Required] string Message,
    string PreferredContact,
    int? PropertyId
);

public record InquiryDto(
    int Id,
    string Name,
    string Phone,
    string? Email,
    string Message,
    string PreferredContact,
    bool IsRead,
    int? PropertyId,
    string? PropertyTitle,
    DateTime CreatedAt
);

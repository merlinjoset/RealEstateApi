using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record TestimonialDto(
    int Id,
    string Name,
    string Location,
    string? PropertyDetail,
    int Rating,
    string Excerpt,
    string? Thumbnail,
    string? VideoUrl,
    string? Duration,
    bool IsPublished,
    int Order,
    DateTime CreatedAt
);

public record CreateTestimonialRequest(
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(120)] string Location,
    string? PropertyDetail,
    [Range(1, 5)] int Rating,
    [Required, MaxLength(1000)] string Excerpt,
    string? Thumbnail,
    string? VideoUrl,
    string? Duration,
    bool IsPublished
);

public record UpdateTestimonialRequest(
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(120)] string Location,
    string? PropertyDetail,
    [Range(1, 5)] int Rating,
    [Required, MaxLength(1000)] string Excerpt,
    string? Thumbnail,
    string? VideoUrl,
    string? Duration,
    bool IsPublished,
    int Order
);

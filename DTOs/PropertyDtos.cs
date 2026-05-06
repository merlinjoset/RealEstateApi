using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public class PropertyQueryParams
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? PropertyType { get; set; }
    public string? Status { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinAreaCents { get; set; }
    public decimal? MaxAreaCents { get; set; }
    public bool? RoadAccess { get; set; }
    public string? SortBy { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public record PropertyDto(
    int Id,
    string Title,
    string Description,
    decimal TotalPrice,
    decimal? PricePerCent,
    string Address,
    string City,
    string District,
    string State,
    string PinCode,
    decimal AreaInCents,
    decimal? AreaInSqFt,
    int? Bedrooms,
    int? Bathrooms,
    string PropertyType,
    string Status,
    List<string> Images,
    List<string> Features,
    List<string> NearbyLandmarks,
    string? LegalStatus,
    bool RoadAccess,
    bool IsFeatured,
    bool IsVerified,
    string ApprovalStatus,
    double? Latitude,
    double? Longitude,
    int? AgentId,
    string? SubmittedByName,
    string? SubmittedByPhone,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreatePropertyRequest(
    [Required, MaxLength(200)] string Title,
    [Required] string Description,
    [Required, Range(1, double.MaxValue)] decimal TotalPrice,
    decimal? PricePerCent,
    [Required] string Address,
    [Required] string City,
    string District,
    string State,
    string PinCode,
    [Required, Range(0.01, 10000)] decimal AreaInCents,
    decimal? AreaInSqFt,
    int? Bedrooms,
    int? Bathrooms,
    [Required] string PropertyType,
    string Status,
    List<string>? Images,
    List<string>? Features,
    List<string>? NearbyLandmarks,
    string? LegalStatus,
    bool RoadAccess,
    bool IsFeatured,
    double? Latitude,
    double? Longitude
);

public record UpdatePropertyRequest(
    string? Title,
    string? Description,
    decimal? TotalPrice,
    decimal? PricePerCent,
    string? Address,
    string? City,
    decimal? AreaInCents,
    decimal? AreaInSqFt,
    int? Bedrooms,
    int? Bathrooms,
    string? PropertyType,
    string? Status,
    List<string>? Images,
    List<string>? Features,
    List<string>? NearbyLandmarks,
    string? LegalStatus,
    bool? RoadAccess,
    bool? IsFeatured,
    bool? IsVerified,
    double? Latitude,
    double? Longitude
);

public record PaginatedResponse<T>(
    List<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

public record ApprovalRequest(
    [Required] string Action,  // "approve" | "reject"
    string? Reason
);

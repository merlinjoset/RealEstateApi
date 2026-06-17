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
    /// <summary>"Free" or "VideoPromotion" — when set, restrict the list to that tier.</summary>
    public string? MarketingPlan { get; set; }
    public string? SortBy { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    // Geolocation filter: when all three are set, only properties whose
    // latitude / longitude lie within RadiusM metres of (NearLat, NearLng)
    // are returned. Rows without coordinates are excluded.
    public double? NearLat { get; set; }
    public double? NearLng { get; set; }
    public double? RadiusM { get; set; }
}

public record PropertyDto(
    int Id,
    string? SerialNo,
    string Title,
    string Description,
    decimal TotalPrice,
    decimal? PricePerCent,
    decimal? DiscountPrice,
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
    string MarketingPlan,
    double? Latitude,
    double? Longitude,
    int? AgentId,
    string? SubmittedByName,
    string? SubmittedByPhone,
    string? SubmittedByEmail,
    int? AssignedToVerifyUserId,
    string? AssignedToVerifyName,
    DateTime? AssignedToVerifyAt,
    string? VerificationNotes,
    DateTime? VerificationDoneAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CityCountDto(string City, int Count);

public record AssignPropertyRequest([Required] int AssignedToUserId);

public record SubmitVerificationRequest([Required] string Notes);

public record CreatePropertyRequest(
    /// <summary>Optional human-facing serial / reference (e.g. "JFL-2026-001").</summary>
    string? SerialNo,
    [Required, MaxLength(200)] string Title,
    [Required] string Description,
    [Required, Range(1, double.MaxValue)] decimal TotalPrice,
    decimal? PricePerCent,
    /// <summary>Optional discounted price; less than TotalPrice. 0 / null = no discount.</summary>
    decimal? DiscountPrice,
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
    /// <summary>"Free" or "VideoPromotion" — defaults to "Free" when omitted.</summary>
    string? MarketingPlan,
    double? Latitude,
    double? Longitude,
    // Submitter contact (only used when called by an unauthenticated visitor)
    string? SubmitterName,
    string? SubmitterPhone,
    string? SubmitterEmail
);

public record UpdatePropertyRequest(
    string? SerialNo,
    string? Title,
    string? Description,
    decimal? TotalPrice,
    decimal? PricePerCent,
    /// <summary>Discounted price. Send a value > 0 to set, or 0 to clear it.</summary>
    decimal? DiscountPrice,
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
    string? MarketingPlan,
    double? Latitude,
    double? Longitude,
    // Owner / seller contact for this listing. Admins can correct these on the
    // edit screen (e.g. when a property was submitted by one account on behalf
    // of a different owner). Null leaves a field untouched; empty string clears.
    string? SubmitterName,
    string? SubmitterPhone,
    string? SubmitterEmail
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

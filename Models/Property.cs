namespace RealEstateApi.Models;

public enum PropertyType
{
    OpenLand,
    LandWithBuilding,
    Agricultural,
    Commercial,
    ResidentialPlot
}

public enum ListingStatus { ForSale, ForRent, Sold }

/// <summary>
/// Seller's chosen marketing tier at submission time.
/// Free   — zero brokerage, basic listing.
/// VideoPromotion — 2% brokerage on the sale price; we shoot a promotional
/// video and feature it across our channels.
/// </summary>
public enum MarketingPlan { Free, VideoPromotion }

public class Property : ISoftDeletable
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public decimal? PricePerCent { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = "Kanyakumari";
    public string State { get; set; } = "Tamil Nadu";
    public string PinCode { get; set; } = string.Empty;
    public decimal AreaInCents { get; set; }
    public decimal? AreaInSqFt { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public PropertyType PropertyType { get; set; } = PropertyType.OpenLand;
    public ListingStatus Status { get; set; } = ListingStatus.ForSale;
    public List<string> Images { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public List<string> NearbyLandmarks { get; set; } = new();
    public string? LegalStatus { get; set; }
    public bool RoadAccess { get; set; }
    public bool IsFeatured { get; set; }
    /// <summary>Free vs paid (Video Promotion, 2% brokerage). Defaults to Free.</summary>
    public MarketingPlan MarketingPlan { get; set; } = MarketingPlan.Free;
    public bool IsVerified { get; set; }
    public bool IsApproved { get; set; } = true;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? RejectionReason { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? AgentId { get; set; }
    public User? Agent { get; set; }
    public int? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }

    /// <summary>
    /// Employee/Agent assigned by an Admin to verify the property — site
    /// visit, document check, photo capture. Null until an admin assigns
    /// someone from the Pending Approvals queue.
    /// </summary>
    public int? AssignedToVerifyUserId { get; set; }
    public User? AssignedToVerifyUser { get; set; }
    public DateTime? AssignedToVerifyAt { get; set; }

    /// <summary>
    /// Free-form findings from the assigned verifier — site-visit notes,
    /// document status, photo links, anything the Admin needs to decide
    /// whether to approve or reject.
    /// </summary>
    public string? VerificationNotes { get; set; }
    /// <summary>Timestamp of the last verification-notes submission.</summary>
    public DateTime? VerificationDoneAt { get; set; }
    // Anonymous submitter contact (used when SubmittedByUser is null —
    // someone submitted via the public /sell page without registering).
    public string? SubmitterName { get; set; }
    public string? SubmitterPhone { get; set; }
    public string? SubmitterEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
    public ICollection<SavedProperty> SavedByUsers { get; set; } = new List<SavedProperty>();
}

public enum ApprovalStatus { Pending, Approved, Rejected }

public class SavedProperty
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

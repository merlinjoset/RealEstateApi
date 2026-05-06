namespace RealEstateApi.Models;

public enum PreferredContact { Phone, WhatsApp }

public enum InquiryStatus { New, Assigned, InProgress, Resolved, Closed }

/// <summary>
/// Categorises inquiries so admins can filter (e.g. all document requests for a property)
/// and so SMS templates can be tailored per type.
/// </summary>
public enum InquiryType
{
    General,         // Any other question
    DocumentRequest, // "Show me the EC / Patta / Chitta"
    SiteVisit,       // "I want to schedule a site visit"
    Pricing,         // "Is the price negotiable?"
    Sell,            // "I want to list a property"
}

public class Inquiry : ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Message { get; set; } = string.Empty;
    public PreferredContact PreferredContact { get; set; } = PreferredContact.Phone;
    public InquiryType Type { get; set; } = InquiryType.General;
    public bool IsRead { get; set; }
    public InquiryStatus Status { get; set; } = InquiryStatus.New;
    public string? Notes { get; set; }                      // employee/admin update notes

    // Property/Buyer relationships
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

    // Assignment workflow
    public int? AssignedToUserId { get; set; }              // employee assigned by admin
    public User? AssignedToUser { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int? AssignedByUserId { get; set; }              // admin who did the assigning
    public int? LastUpdatedByUserId { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

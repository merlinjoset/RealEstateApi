namespace RealEstateApi.Models;

/// <summary>
/// Stored SMS template body with named placeholders like {name}, {title}.
/// Looked up by Key — code never hard-codes the body string.
/// </summary>
public class SmsTemplate : ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>Stable identifier the code uses, e.g. "inquiry.confirmation".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-friendly label shown in the admin UI.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Short description of when this fires.</summary>
    public string? Description { get; set; }

    /// <summary>The SMS body. Placeholders use curly braces: {name}, {title}.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Comma-separated list of supported placeholders for the editor.</summary>
    public string? AvailableVars { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

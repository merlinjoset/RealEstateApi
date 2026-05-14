namespace RealEstateApi.Models;

/// <summary>
/// Singleton row holding admin-editable site-wide settings. Always exactly
/// one row (Id = 1), seeded by the InitialSiteSettings migration. Used today
/// for the social profile URLs the public Footer renders; can grow to hold
/// other config the AdminSettings page exposes later (contact phones,
/// office hours, brand copy, etc.) without further table changes.
/// </summary>
public class SiteSetting
{
    public int Id { get; set; }

    public string FacebookUrl  { get; set; } = "";
    public string InstagramUrl { get; set; } = "";
    public string YoutubeUrl   { get; set; } = "";
    public string WebsiteUrl   { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

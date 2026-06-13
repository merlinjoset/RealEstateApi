namespace RealEstateApi.Models;

/// <summary>
/// A single anonymous page view, recorded by the SPA on every route change.
/// Deliberately PII-free: no IP address, no full user-agent, no account link.
/// `VisitorId` is a random client-generated token (localStorage) used only to
/// approximate unique-visitor counts — it identifies a browser, not a person.
/// </summary>
public class PageView
{
    public long Id { get; set; }

    /// <summary>The in-app path visited, e.g. "/properties/461" (query stripped).</summary>
    public string Path { get; set; } = "";

    /// <summary>Referrer host only (e.g. "google.com"); null/empty = direct.</summary>
    public string? Referrer { get; set; }

    /// <summary>Anonymous random per-browser token for unique-visitor counts.</summary>
    public string? VisitorId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

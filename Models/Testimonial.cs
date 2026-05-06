namespace RealEstateApi.Models;

public class Testimonial : ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;        // e.g. "Nagercoil"
    public string? PropertyDetail { get; set; }                  // e.g. "15 cents · Open Land"
    public int Rating { get; set; } = 5;                         // 1..5
    public string Excerpt { get; set; } = string.Empty;          // short pull-quote
    public string? Thumbnail { get; set; }                       // image URL
    public string? VideoUrl { get; set; }                        // mp4 / embed URL
    public string? Duration { get; set; }                        // e.g. "1:24"
    public bool IsPublished { get; set; } = true;
    public int Order { get; set; }                               // display order
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

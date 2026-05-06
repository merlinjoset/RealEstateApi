namespace RealEstateApi.Models;

public enum PreferredContact { Phone, WhatsApp }

public class Inquiry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Message { get; set; } = string.Empty;
    public PreferredContact PreferredContact { get; set; } = PreferredContact.Phone;
    public bool IsRead { get; set; }
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

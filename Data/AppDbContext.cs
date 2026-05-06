using Microsoft.EntityFrameworkCore;
using RealEstateApi.Models;

namespace RealEstateApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SavedProperty> SavedProperties => Set<SavedProperty>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // User
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();
        });

        // Property
        mb.Entity<Property>(e =>
        {
            e.Property(p => p.PropertyType).HasConversion<string>();
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.ApprovalStatus).HasConversion<string>();
            e.Property(p => p.Images).HasColumnType("text[]");
            e.Property(p => p.Features).HasColumnType("text[]");
            e.Property(p => p.NearbyLandmarks).HasColumnType("text[]");
            e.Property(p => p.TotalPrice).HasPrecision(18, 2);
            e.Property(p => p.PricePerCent).HasPrecision(18, 2);
            e.Property(p => p.AreaInCents).HasPrecision(10, 3);
            e.Property(p => p.AreaInSqFt).HasPrecision(12, 2);

            e.HasOne(p => p.Agent)
             .WithMany()
             .HasForeignKey(p => p.AgentId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.SubmittedByUser)
             .WithMany()
             .HasForeignKey(p => p.SubmittedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // SavedProperty (many-to-many join)
        mb.Entity<SavedProperty>(e =>
        {
            e.HasKey(sp => new { sp.UserId, sp.PropertyId });
            e.HasOne(sp => sp.User).WithMany(u => u.SavedProperties).HasForeignKey(sp => sp.UserId);
            e.HasOne(sp => sp.Property).WithMany(p => p.SavedByUsers).HasForeignKey(sp => sp.PropertyId);
        });

        // RefreshToken
        mb.Entity<RefreshToken>(e =>
        {
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId);
        });

        // Inquiry
        mb.Entity<Inquiry>(e =>
        {
            e.Property(i => i.PreferredContact).HasConversion<string>();
            e.HasOne(i => i.Property).WithMany(p => p.Inquiries).HasForeignKey(i => i.PropertyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.User).WithMany(u => u.Inquiries).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        SeedData(mb);
    }

    private static void SeedData(ModelBuilder mb)
    {
        mb.Entity<User>().HasData(new User
        {
            Id = 1,
            FirstName = "Admin",
            LastName = "Jose",
            Email = "admin@joseforland.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin,
            Phone = "+919994488490",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var properties = new[]
        {
            new Property { Id = 1, Title = "15 Cents Prime Land Near Highway – Nagercoil", Description = "Prime land parcel near NH 44 with excellent road frontage. Clear legal documents available.", TotalPrice = 2250000, PricePerCent = 150000, Address = "Kottar, Near NH 44", City = "Nagercoil", PinCode = "629001", AreaInCents = 15, AreaInSqFt = 6534, PropertyType = PropertyType.OpenLand, Status = ListingStatus.ForSale, Images = new List<string>(), Features = new List<string> { "Road Access", "Clear Title", "Near Market" }, NearbyLandmarks = new List<string> { "Nagercoil Railway Station (2 km)", "NH 44 (200 m)" }, LegalStatus = "Clear – EC, Patta, Chitta available", RoadAccess = true, IsFeatured = true, IsVerified = true, ApprovalStatus = ApprovalStatus.Approved, CreatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Property { Id = 2, Title = "10 Cents Land with 3BHK House – Marthandam", Description = "Ready-to-occupy land with a well-built 3BHK house. All amenities available.", TotalPrice = 4500000, PricePerCent = 450000, Address = "Town Center", City = "Marthandam", PinCode = "629165", AreaInCents = 10, Bedrooms = 3, Bathrooms = 2, PropertyType = PropertyType.LandWithBuilding, Status = ListingStatus.ForSale, Images = new List<string>(), Features = new List<string> { "Road Access", "Electricity", "Water Source" }, NearbyLandmarks = new List<string> { "Marthandam Bus Stand (500 m)" }, RoadAccess = true, IsFeatured = true, IsVerified = true, ApprovalStatus = ApprovalStatus.Approved, CreatedAt = new DateTime(2024, 1, 16, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 16, 0, 0, 0, DateTimeKind.Utc) },
            new Property { Id = 3, Title = "50 Cents Agricultural Land – Thuckalay", Description = "Fertile agricultural land with natural water source. Ideal for farming.", TotalPrice = 3500000, PricePerCent = 70000, Address = "Pechipparai Road", City = "Thuckalay", PinCode = "629175", AreaInCents = 50, PropertyType = PropertyType.Agricultural, Status = ListingStatus.ForSale, Images = new List<string>(), Features = new List<string> { "Water Source", "Fertile Soil" }, NearbyLandmarks = new List<string>(), RoadAccess = false, IsFeatured = false, IsVerified = true, ApprovalStatus = ApprovalStatus.Approved, CreatedAt = new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 17, 0, 0, 0, DateTimeKind.Utc) },
        };

        mb.Entity<Property>().HasData(properties);
    }
}

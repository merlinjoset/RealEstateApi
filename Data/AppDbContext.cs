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
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();

    /// <summary>
    /// Override SaveChanges to convert any Remove() calls on ISoftDeletable
    /// entities into a soft-delete (IsDeleted=true) instead of a real delete.
    /// </summary>
    public override int SaveChanges()
    {
        ApplySoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplySoftDelete();
        return base.SaveChangesAsync(ct);
    }

    private void ApplySoftDelete()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Global soft-delete query filter — exclude IsDeleted rows from every query.
        mb.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        mb.Entity<Property>().HasQueryFilter(p => !p.IsDeleted);
        mb.Entity<Inquiry>().HasQueryFilter(i => !i.IsDeleted);
        mb.Entity<Testimonial>().HasQueryFilter(t => !t.IsDeleted);
        mb.Entity<SmsTemplate>().HasQueryFilter(t => !t.IsDeleted);

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

        // SmsTemplate
        mb.Entity<SmsTemplate>(e =>
        {
            e.HasIndex(t => t.Key).IsUnique();
        });

        // Inquiry
        mb.Entity<Inquiry>(e =>
        {
            e.Property(i => i.PreferredContact).HasConversion<string>();
            e.Property(i => i.Type).HasConversion<string>();
            e.Property(i => i.Status).HasConversion<string>();
            e.HasOne(i => i.Property).WithMany(p => p.Inquiries).HasForeignKey(i => i.PropertyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.User).WithMany(u => u.Inquiries).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.AssignedToUser).WithMany().HasForeignKey(i => i.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
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

        // SMS templates seed — every SMS the app sends has a key here.
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        mb.Entity<SmsTemplate>().HasData(
            new SmsTemplate { Id = 1, Key = "inquiry.confirmation", Label = "Inquiry — visitor confirmation",
                Description = "Sent to the public visitor right after they submit an inquiry.",
                Body = "Hi {name}, thank you for reaching Jose For Land. Our team will call you back within 2-5 hours. For urgent help, dial +91 99944 88490.",
                AvailableVars = "name,phone,propertyId",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 2, Key = "inquiry.adminNotification", Label = "Inquiry — admin alert",
                Description = "Notifies the admin team whenever a new inquiry comes in.",
                Body = "📩 New inquiry from {name} ({phone}){propertyContext}. Check the admin panel.",
                AvailableVars = "name,phone,propertyContext",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 3, Key = "inquiry.assignment", Label = "Inquiry — assigned to employee",
                Description = "Notifies an employee that an inquiry has been assigned to them.",
                Body = "📨 New inquiry assigned to you: {name} ({phone}). Open the admin panel to review.",
                AvailableVars = "name,phone",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 4, Key = "inquiry.statusUpdate", Label = "Inquiry — status changed",
                Description = "Notifies admins when an employee updates an inquiry status.",
                Body = "🔔 Inquiry #{id} ({name}) updated by {actor}: {prevStatus} → {newStatus}{noteSuffix}",
                AvailableVars = "id,name,actor,prevStatus,newStatus,noteSuffix",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 5, Key = "property.submittedConfirmation", Label = "Property — submitter confirmation",
                Description = "Sent to a seller after they submit a property for review.",
                Body = "Hi {name}, thank you for submitting '{title}' on Jose For Land. Our team will review it within 24 hours and get back to you.",
                AvailableVars = "name,title",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 6, Key = "property.adminPending", Label = "Property — admin pending alert",
                Description = "Notifies admins of a new property submission awaiting approval.",
                Body = "🏡 New property pending: '{title}' (₹{priceLakhs}L / {area} cents) from {name} ({phone})",
                AvailableVars = "title,priceLakhs,area,name,phone",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 7, Key = "property.approved", Label = "Property — approved (LIVE)",
                Description = "Sent to the seller when their property is approved by an admin.",
                Body = "🎉 Hi {name}, your property '{title}' is now LIVE on Jose For Land! Buyers can now view it. Visit joseforland.com to see it online.",
                AvailableVars = "name,title",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 8, Key = "property.rejected", Label = "Property — rejected",
                Description = "Sent to the seller if their property is rejected by an admin.",
                Body = "Hi {name}, your submission '{title}' could not be approved.{reasonSuffix} Call +91 99944 88490 for help.",
                AvailableVars = "name,title,reasonSuffix",
                CreatedAt = seedDate, UpdatedAt = seedDate },
            new SmsTemplate { Id = 9, Key = "inquiry.documentRequest", Label = "Inquiry — document request alert",
                Description = "Notifies admins when a buyer requests verified documents for a property.",
                Body = "📄 DOCUMENT REQUEST from {name} ({phone}) for property #{propertyId}. They want to view EC/Patta/Chitta etc. Call them within 2-5 hrs with copies.",
                AvailableVars = "name,phone,propertyId,type",
                CreatedAt = seedDate, UpdatedAt = seedDate }
        );

        // Testimonials seed
        mb.Entity<Testimonial>().HasData(
            new Testimonial
            {
                Id = 1, Name = "Rajan Kumar", Location = "Nagercoil",
                PropertyDetail = "15 cents · Open Land", Rating = 5,
                Excerpt = "They visited the site with us, explained every document, and stayed honest throughout. Bought my first land through Jose For Land — no regrets.",
                Thumbnail = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=900&q=80&auto=format&fit=crop",
                VideoUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4",
                Duration = "1:24", IsPublished = true, Order = 1,
                CreatedAt = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc),
            },
            new Testimonial
            {
                Id = 2, Name = "Priya Selvam", Location = "Marthandam",
                PropertyDetail = "10 cents · Residential Plot", Rating = 5,
                Excerpt = "The free doorstep consultation is real — they came to our village, walked the plot with us, and answered every question. Genuine team.",
                Thumbnail = "https://images.unsplash.com/photo-1464082354059-27db6ce50048?w=900&q=80&auto=format&fit=crop",
                VideoUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4",
                Duration = "0:58", IsPublished = true, Order = 2,
                CreatedAt = new DateTime(2024, 1, 18, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 18, 0, 0, 0, DateTimeKind.Utc),
            },
            new Testimonial
            {
                Id = 3, Name = "Xavier Joseph", Location = "Colachel",
                PropertyDetail = "50 cents · Agricultural", Rating = 5,
                Excerpt = "Compared with three other agents — Jose For Land had the cleanest documentation and the most transparent pricing. Highly recommend.",
                Thumbnail = "https://images.unsplash.com/photo-1500076656116-558758c991c1?w=900&q=80&auto=format&fit=crop",
                VideoUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
                Duration = "1:42", IsPublished = true, Order = 3,
                CreatedAt = new DateTime(2024, 1, 22, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 22, 0, 0, 0, DateTimeKind.Utc),
            }
        );
    }
}

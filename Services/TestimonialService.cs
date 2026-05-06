using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface ITestimonialService
{
    Task<IReadOnlyList<TestimonialDto>> GetAllAsync(bool publishedOnly);
    Task<TestimonialDto?> GetByIdAsync(int id);
    Task<TestimonialDto> CreateAsync(CreateTestimonialRequest req);
    Task<TestimonialDto?> UpdateAsync(int id, UpdateTestimonialRequest req);
    Task<TestimonialDto?> TogglePublishedAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class TestimonialService(AppDbContext db) : ITestimonialService
{
    private static TestimonialDto ToDto(Testimonial t) => new(
        t.Id, t.Name, t.Location, t.PropertyDetail, t.Rating, t.Excerpt,
        t.Thumbnail, t.VideoUrl, t.Duration, t.IsPublished, t.Order, t.CreatedAt
    );

    public async Task<IReadOnlyList<TestimonialDto>> GetAllAsync(bool publishedOnly)
    {
        var query = db.Testimonials.AsQueryable();
        if (publishedOnly) query = query.Where(t => t.IsPublished);

        var list = await query
            .OrderBy(t => t.Order)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    public async Task<TestimonialDto?> GetByIdAsync(int id)
    {
        var t = await db.Testimonials.FindAsync(id);
        return t is null ? null : ToDto(t);
    }

    public async Task<TestimonialDto> CreateAsync(CreateTestimonialRequest req)
    {
        var maxOrder = await db.Testimonials.AnyAsync()
            ? await db.Testimonials.MaxAsync(t => (int?)t.Order) ?? 0
            : 0;

        var t = new Testimonial
        {
            Name = req.Name,
            Location = req.Location,
            PropertyDetail = req.PropertyDetail,
            Rating = req.Rating,
            Excerpt = req.Excerpt,
            Thumbnail = req.Thumbnail,
            VideoUrl = req.VideoUrl,
            Duration = req.Duration,
            IsPublished = req.IsPublished,
            Order = maxOrder + 1,
        };

        db.Testimonials.Add(t);
        await db.SaveChangesAsync();
        return ToDto(t);
    }

    public async Task<TestimonialDto?> UpdateAsync(int id, UpdateTestimonialRequest req)
    {
        var t = await db.Testimonials.FindAsync(id);
        if (t is null) return null;

        t.Name = req.Name;
        t.Location = req.Location;
        t.PropertyDetail = req.PropertyDetail;
        t.Rating = req.Rating;
        t.Excerpt = req.Excerpt;
        t.Thumbnail = req.Thumbnail;
        t.VideoUrl = req.VideoUrl;
        t.Duration = req.Duration;
        t.IsPublished = req.IsPublished;
        t.Order = req.Order;
        t.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(t);
    }

    public async Task<TestimonialDto?> TogglePublishedAsync(int id)
    {
        var t = await db.Testimonials.FindAsync(id);
        if (t is null) return null;
        t.IsPublished = !t.IsPublished;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(t);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var t = await db.Testimonials.FindAsync(id);
        if (t is null) return false;
        db.Testimonials.Remove(t);
        await db.SaveChangesAsync();
        return true;
    }
}

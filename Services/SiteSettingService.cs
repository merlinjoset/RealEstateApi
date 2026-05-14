using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface ISiteSettingService
{
    Task<SiteSettingDto> GetAsync();
    Task<SiteSettingDto> UpdateAsync(UpdateSiteSettingRequest req);
}

public class SiteSettingService(AppDbContext db) : ISiteSettingService
{
    /// <summary>Read the singleton settings row. Always returns a row — if the
    /// table is empty (rare, only when seed didn't run) we fall back to the
    /// hard-coded defaults so the caller never deals with null.</summary>
    public async Task<SiteSettingDto> GetAsync()
    {
        var s = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
        if (s is null)
        {
            return new SiteSettingDto(
                "https://facebook.com/joseforland",
                "https://instagram.com/joseforland",
                "https://youtube.com/@joseforland",
                "",
                DateTime.UtcNow);
        }
        return ToDto(s);
    }

    public async Task<SiteSettingDto> UpdateAsync(UpdateSiteSettingRequest req)
    {
        var s = await db.SiteSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            // First-time write — bootstrap the singleton row.
            s = new SiteSetting { Id = 1 };
            db.SiteSettings.Add(s);
        }

        // Null inputs leave the existing value alone — admin can update one
        // field without having to re-submit the others.
        if (req.FacebookUrl  is not null) s.FacebookUrl  = req.FacebookUrl.Trim();
        if (req.InstagramUrl is not null) s.InstagramUrl = req.InstagramUrl.Trim();
        if (req.YoutubeUrl   is not null) s.YoutubeUrl   = req.YoutubeUrl.Trim();
        if (req.WebsiteUrl   is not null) s.WebsiteUrl   = req.WebsiteUrl.Trim();
        s.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(s);
    }

    private static SiteSettingDto ToDto(SiteSetting s) =>
        new(s.FacebookUrl, s.InstagramUrl, s.YoutubeUrl, s.WebsiteUrl, s.UpdatedAt);
}

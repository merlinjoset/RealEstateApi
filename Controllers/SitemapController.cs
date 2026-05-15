using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Models;

namespace RealEstateApi.Controllers;

/// <summary>
/// Renders sitemap.xml for search-engine crawlers. The frontend's nginx
/// config rewrites /sitemap.xml + /sitemap-properties.xml to these
/// endpoints so the canonical URL search engines see is at the public
/// root (e.g. https://joseforland.com/sitemap.xml).
/// </summary>
[ApiController]
public class SitemapController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private string SiteBase => (config["Seo:SiteBaseUrl"] ?? "https://joseforland.com").TrimEnd('/');

    /// <summary>
    /// Sitemap index — references the static pages + the dynamic property
    /// sitemap. Keeping properties in a separate file keeps each file
    /// under Google's 50,000-URL / 50 MB limit even at scale.
    /// </summary>
    [HttpGet("/sitemap.xml")]
    [HttpGet("/api/seo/sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        var staticPages = new[] { "/", "/properties", "/map", "/about", "/contact", "/sell" };
        // newest CreatedAt across approved properties = effective last-modified
        // for the property sitemap.
        var propsLastMod = await db.Properties
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => (DateTime?)p.UpdatedAt)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync() ?? DateTime.UtcNow;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var path in staticPages)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{Escape(SiteBase + path)}</loc>");
            sb.AppendLine($"    <changefreq>{(path == "/" ? "daily" : "weekly")}</changefreq>");
            sb.AppendLine($"    <priority>{(path == "/" ? "1.0" : "0.7")}</priority>");
            sb.AppendLine("  </url>");
        }
        // Reference the property sitemap as a regular URL too — actual property
        // URLs land in /sitemap-properties.xml below.
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{Escape(SiteBase + "/sitemap-properties.xml")}</loc>");
        sb.AppendLine($"    <lastmod>{propsLastMod:yyyy-MM-dd}</lastmod>");
        sb.AppendLine("  </url>");
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    /// <summary>
    /// Every approved property gets a <c>&lt;url&gt;</c> entry with its
    /// updated-at as <c>&lt;lastmod&gt;</c>. Crawlers re-fetch only when
    /// the date changes, so this is cheap even when polled frequently.
    /// </summary>
    [HttpGet("/sitemap-properties.xml")]
    [HttpGet("/api/seo/sitemap-properties.xml")]
    public async Task<IActionResult> Properties()
    {
        var properties = await db.Properties
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => new { p.Id, p.UpdatedAt })
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var p in properties)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{Escape($"{SiteBase}/properties/{p.Id}")}</loc>");
            sb.AppendLine($"    <lastmod>{p.UpdatedAt:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>weekly</changefreq>");
            sb.AppendLine("    <priority>0.8</priority>");
            sb.AppendLine("  </url>");
        }
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private static string Escape(string raw) =>
        // XML-escape the URL for safety even though we control the input today.
        new System.Xml.Linq.XText(raw).ToString();
}

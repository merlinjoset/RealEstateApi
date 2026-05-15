using System.Globalization;
using System.Text.RegularExpressions;
using RealEstateApi.Models;

namespace EstatikImporter;

/// <summary>
/// WordPress / Estatik record → our <see cref="Property"/> entity.
///
/// Confirmed against the joseforland.com 2026-05-15 WXR export — keys
/// below match what Estatik actually stores on that install.
/// </summary>
public static class PropertyMapper
{
    /// <summary>The es_property_price_note often spells out the area in
    /// cents (e.g. "7 Cents (10.5 Lakh/cent)"). Pull that out preferentially
    /// since es_property_area is in square feet on this install and the
    /// India-friendly unit is cents.</summary>
    private static readonly Regex CentsInNote = new(
        @"(?<value>\d+(?:\.\d+)?)\s*Cents?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Property? FromWpItem(WpItem item, AttachmentMap attachments)
    {
        // Skip drafts / pending / trashed — only migrate published rows.
        if (item.Status != "publish") return null;

        var price = ParseDecimal(item.GetMeta("es_property_price"));
        if (price is null || price.Value <= 0)
        {
            Console.WriteLine($"  ⚠ skip #{item.PostId} '{Truncate(item.Title, 50)}' (no price)");
            return null;
        }

        var (areaInCents, areaInSqFt) = ParseArea(item);
        if (areaInCents is null)
        {
            Console.WriteLine($"  ⚠ skip #{item.PostId} '{Truncate(item.Title, 50)}' (no area)");
            return null;
        }

        // Estatik's price_note often has nicer "per cent" copy too — surface
        // both that and the alternative description in our description body.
        var description = StripHtml(item.GetMeta("es_property_alternative_description") ?? item.Content);
        var priceNote = item.GetMeta("es_property_price_note")?.Trim();
        if (!string.IsNullOrWhiteSpace(priceNote)
            && !description.Contains(priceNote, StringComparison.OrdinalIgnoreCase))
            description = $"{priceNote}\n\n{description}".Trim();

        // City: prefer the free-text "Thuckalay region" style over the taxonomy
        // ID (which is meaningless without resolving the taxonomy table).
        var city = item.GetMeta("es_property_location")?.Trim();
        if (string.IsNullOrWhiteSpace(city) || IsNumericId(city))
            city = item.GetMeta("es_property_province")?.Trim() ?? "";
        city = TitleCase(city);

        var images = ResolveImages(item, attachments);

        return new Property
        {
            WpPostId        = item.PostId,
            Title           = item.Title.Trim(),
            Description     = description,
            TotalPrice      = price.Value,
            AreaInCents     = areaInCents.Value,
            AreaInSqFt      = areaInSqFt,
            Address         = item.GetMeta("es_property_address")?.Trim() ?? "",
            City            = city,
            District        = "Kanyakumari",
            State           = "Tamil Nadu",
            PinCode         = item.GetMeta("es_property_postal_code")?.Trim() ?? "",
            PropertyType    = MapPropertyType(item.GetMeta("es_property_type")),
            Status          = ListingStatus.ForSale,
            Latitude        = ParseDouble(item.GetMeta("es_property_latitude")),
            Longitude       = ParseDouble(item.GetMeta("es_property_longitude")),
            Bedrooms        = ParseInt(item.GetMeta("es_property_bedrooms")),
            Bathrooms       = null,
            Features        = new List<string>(),
            NearbyLandmarks = new List<string>(),
            Images          = images,
            LegalStatus     = null,
            RoadAccess      = true,    // Estatik export doesn't have this — default true
            IsApproved      = true,
            ApprovalStatus  = ApprovalStatus.Approved,
            MarketingPlan   = MarketingPlan.Free,
            SubmitterName   = item.Creator,
            CreatedAt       = item.PostDate == default ? DateTime.UtcNow : item.PostDate,
            UpdatedAt       = DateTime.UtcNow,
        };
    }

    /// <summary>Copy mapped fields into an existing row so re-imports update
    /// in place rather than duplicating. Preserves Id and FK fields.</summary>
    public static void CopyInto(Property src, Property dest)
    {
        dest.Title = src.Title;
        dest.Description = src.Description;
        dest.TotalPrice = src.TotalPrice;
        dest.AreaInCents = src.AreaInCents;
        dest.AreaInSqFt = src.AreaInSqFt;
        dest.Address = src.Address;
        dest.City = src.City;
        dest.District = src.District;
        dest.State = src.State;
        dest.PinCode = src.PinCode;
        dest.PropertyType = src.PropertyType;
        dest.Status = src.Status;
        dest.Latitude = src.Latitude;
        dest.Longitude = src.Longitude;
        dest.Bedrooms = src.Bedrooms;
        dest.Images = src.Images;
        dest.UpdatedAt = DateTime.UtcNow;
    }

    /* ── Area parsing ────────────────────────────────────────────────────── */

    /// <summary>
    /// Returns (areaInCents, areaInSqFt).
    /// Strategy:
    ///   1. Look for "N Cents" in es_property_price_note — most accurate.
    ///   2. Look for "N Cents" in es_property_keywords.
    ///   3. Look for "N Cents" anywhere in the description.
    ///   4. Fall back to es_property_area, interpreted as sq ft (and converted to cents).
    /// </summary>
    private static (decimal? cents, decimal? sqFt) ParseArea(WpItem item)
    {
        var note = item.GetMeta("es_property_price_note") ?? "";
        var keywords = item.GetMeta("es_property_keywords") ?? "";
        var alt = item.GetMeta("es_property_alternative_description") ?? "";

        foreach (var src in new[] { note, keywords, alt, item.Content })
        {
            var m = CentsInNote.Match(src);
            if (m.Success
                && decimal.TryParse(m.Groups["value"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var cents)
                && cents > 0 && cents < 100_000)
            {
                // Also pick up the sq-ft value if Estatik stored one — purely
                // informational on our side.
                var sqFt = ParseDecimal(item.GetMeta("es_property_area"));
                return (cents, sqFt);
            }
        }

        // Fallback: es_property_area in sq ft → cents.
        var raw = ParseDecimal(item.GetMeta("es_property_area"));
        if (raw is null || raw.Value <= 0) return (null, null);
        return (Math.Round(raw.Value / 435.6m, 2), raw.Value);
    }

    /* ── Image resolution ───────────────────────────────────────────────── */

    private static List<string> ResolveImages(WpItem item, AttachmentMap attachments)
    {
        // Order matters — featured image (_thumbnail_id) goes first so it
        // becomes the property's hero image / OG share image.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new List<string>();
        void add(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (seen.Add(url)) urls.Add(url);
        }

        // _thumbnail_id is an attachment ID
        if (int.TryParse(item.GetMeta("_thumbnail_id"), out var thumbId))
            add(attachments.UrlFor(thumbId));

        // es_property_gallery on this install is a single ID (568 properties,
        // checked). Other installs sometimes store CSV of IDs — handle both.
        var gallery = item.GetMeta("es_property_gallery");
        if (!string.IsNullOrWhiteSpace(gallery))
        {
            foreach (var part in gallery.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var id))
                    add(attachments.UrlFor(id));
        }
        return urls;
    }

    /* ── Property-type mapping ──────────────────────────────────────────── */

    private static PropertyType MapPropertyType(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant();
        if (v.Contains("agricultur") || v.Contains("farm")) return PropertyType.Agricultural;
        if (v.Contains("commercial")) return PropertyType.Commercial;
        if (v.Contains("villa") || v.Contains("house") || v.Contains("building")) return PropertyType.LandWithBuilding;
        if (v.Contains("resident") || v.Contains("plot")) return PropertyType.ResidentialPlot;
        return PropertyType.OpenLand;
    }

    /* ── Primitives ─────────────────────────────────────────────────────── */

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static double? ParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, out var v) ? v : null;
    }

    private static bool IsNumericId(string s) =>
        !string.IsNullOrEmpty(s) && s.All(char.IsDigit);

    private static string TitleCase(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // "thuckalay" → "Thuckalay", "Thuckalay region" → "Thuckalay Region"
        return CultureInfo.GetCultureInfo("en-IN").TextInfo
            .ToTitleCase(raw.Trim().ToLowerInvariant());
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var noShortcodes = Regex.Replace(html, @"\[[^\]]+\]", "");
        var noTags = Regex.Replace(noShortcodes, @"<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        var collapsedWs = Regex.Replace(decoded, @"[ \t]+", " ");
        return collapsedWs.Trim();
    }
}

/// <summary>
/// Pre-scan result — maps attachment IDs to their public WordPress URLs.
/// Built by reading every `attachment` post type item once before the
/// property pass, then queried as each property's _thumbnail_id and
/// es_property_gallery are resolved.
/// </summary>
public class AttachmentMap
{
    private readonly Dictionary<int, string> _byId = new();
    public string SiteBaseUrl { get; }

    public AttachmentMap(string siteBaseUrl)
    {
        SiteBaseUrl = siteBaseUrl.TrimEnd('/');
    }

    public void Add(int postId, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        // _wp_attached_file is relative to wp-content/uploads/, e.g.
        //   "2023/04/image2.jpg" → "https://joseforland.com/wp-content/uploads/2023/04/image2.jpg"
        _byId[postId] = $"{SiteBaseUrl}/wp-content/uploads/{relativePath.TrimStart('/')}";
    }

    public string? UrlFor(int postId) =>
        _byId.TryGetValue(postId, out var url) ? url : null;

    public int Count => _byId.Count;
}

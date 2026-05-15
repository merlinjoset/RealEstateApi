using System.Globalization;
using RealEstateApi.Models;

namespace EstatikImporter;

/// <summary>
/// WordPress / Estatik record → our <see cref="Property"/> entity.
///
/// ⚠ The meta-key names below are the *best-guess* defaults for Estatik. The
/// actual keys vary between Estatik versions and per-site customisations —
/// run the importer in --analyze mode first to see the real keys for the
/// site being migrated, then patch this file before --import.
/// </summary>
public static class PropertyMapper
{
    public static Property? FromWpItem(WpItem item)
    {
        // Skip drafts / trashed entries — only migrate published listings.
        if (item.Status is not ("publish" or "publish_pending"))
            return null;

        var price = ParseDecimal(item.GetMeta("_es_price", "es_price", "price"));
        var area  = ParseDecimal(item.GetMeta("_es_size", "es_size", "size", "area"));
        if (price is null || area is null)
        {
            // Estatik occasionally stores price/size in different units —
            // if both are missing skip the row instead of writing garbage.
            // The analyze pass should tell us which keys are actually populated.
            Console.WriteLine($"  ⚠ skip #{item.PostId} '{item.Title}' (missing price or area)");
            return null;
        }

        var lat = ParseDouble(item.GetMeta("_es_lat", "es_lat", "lat", "latitude"));
        var lng = ParseDouble(item.GetMeta("_es_lng", "es_lng", "lng", "longitude"));

        // Strip rudimentary HTML from the post_content. WordPress stores blocks
        // / shortcodes / inline styles; we want a plain-text description.
        var description = StripHtml(item.Content);

        return new Property
        {
            WpPostId        = item.PostId,
            Title           = item.Title.Trim(),
            Description     = description,
            TotalPrice      = price.Value,
            AreaInCents     = area.Value,
            Address         = item.GetMeta("_es_address", "es_address", "address") ?? "",
            City            = item.GetMeta("_es_city", "es_city", "city") ?? "",
            District        = item.GetMeta("_es_district", "es_district", "district") ?? "Kanyakumari",
            State           = item.GetMeta("_es_state", "es_state", "state") ?? "Tamil Nadu",
            PinCode         = item.GetMeta("_es_zip", "es_zip", "zip", "postal_code") ?? "",
            PropertyType    = MapPropertyType(item),
            Status          = ListingStatus.ForSale,
            Latitude        = lat,
            Longitude       = lng,
            Features        = ParseFeatures(item.GetMeta("_es_features", "es_features", "features")),
            NearbyLandmarks = new List<string>(),
            Images          = ParseImages(item),
            LegalStatus     = item.GetMeta("_es_legal_status", "es_legal_status"),
            RoadAccess      = ParseBool(item.GetMeta("_es_road_access", "es_road_access")) ?? false,
            IsApproved      = true,
            ApprovalStatus  = ApprovalStatus.Approved,
            MarketingPlan   = MarketingPlan.Free,
            SubmitterName   = item.Creator,
            CreatedAt       = item.PostDate == default ? DateTime.UtcNow : item.PostDate,
            UpdatedAt       = DateTime.UtcNow,
        };
    }

    /// <summary>Copy the mapped fields into an existing row so re-imports
    /// update in place rather than duplicating. Preserves Id and FK fields.</summary>
    public static void CopyInto(Property src, Property dest)
    {
        dest.Title = src.Title;
        dest.Description = src.Description;
        dest.TotalPrice = src.TotalPrice;
        dest.AreaInCents = src.AreaInCents;
        dest.Address = src.Address;
        dest.City = src.City;
        dest.District = src.District;
        dest.State = src.State;
        dest.PinCode = src.PinCode;
        dest.PropertyType = src.PropertyType;
        dest.Status = src.Status;
        dest.Latitude = src.Latitude;
        dest.Longitude = src.Longitude;
        dest.Features = src.Features;
        dest.Images = src.Images;
        dest.LegalStatus = src.LegalStatus;
        dest.RoadAccess = src.RoadAccess;
        dest.UpdatedAt = DateTime.UtcNow;
    }

    /* ── Parsers ─────────────────────────────────────────────────────────── */

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

    private static bool? ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim().ToLowerInvariant();
        return v is "1" or "yes" or "true" or "on";
    }

    private static List<string> ParseFeatures(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        // Estatik stores features as comma-separated or serialised PHP — try
        // CSV first, fall back to single value.
        return raw.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .ToList();
    }

    private static List<string> ParseImages(WpItem item)
    {
        // Common Estatik patterns: meta key holds CSV of attachment IDs, OR
        // CSV of direct URLs, OR multiple meta entries like es_image_0,
        // es_image_1. The analyze pass will reveal which one we have.
        var raw = item.GetMeta("_es_image_url", "es_image_url", "_es_images", "es_images");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Where(s => s.StartsWith("http"))
                      .ToList();
        }
        // Numbered keys fallback
        var imgs = new List<string>();
        foreach (var (k, v) in item.Meta)
            if (k.Contains("image", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(v)
                && v.StartsWith("http"))
                imgs.Add(v);
        return imgs;
    }

    private static PropertyType MapPropertyType(WpItem item)
    {
        // Best-guess from Estatik category labels and/or a meta key.
        var raw = (item.GetMeta("_es_category", "es_category", "property_type")
            ?? item.Categories.FirstOrDefault(c => c.Domain.Contains("categor", StringComparison.OrdinalIgnoreCase))?.Label
            ?? "").ToLowerInvariant();

        if (raw.Contains("agricultur")) return PropertyType.Agricultural;
        if (raw.Contains("building") || raw.Contains("house") || raw.Contains("villa")) return PropertyType.LandWithBuilding;
        if (raw.Contains("commercial")) return PropertyType.Commercial;
        if (raw.Contains("resident"))   return PropertyType.ResidentialPlot;
        return PropertyType.OpenLand;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Strip shortcodes [foo a="b"] and HTML tags. Crude but adequate.
        var noShortcodes = System.Text.RegularExpressions.Regex.Replace(html, @"\[[^\]]+\]", "");
        var noTags = System.Text.RegularExpressions.Regex.Replace(noShortcodes, @"<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}

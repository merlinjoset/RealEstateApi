using System.Globalization;
using System.Xml.Linq;

namespace EstatikImporter;

/// <summary>
/// Streams a WordPress WXR (Extended RSS) export and yields one
/// <see cref="WpItem"/> per &lt;item&gt; in the file. Implements the slice
/// of WXR we care about for an Estatik migration — title, content, post_id,
/// post_type, post_status, post_date, plus the full meta-key/meta-value map
/// (where Estatik plugin data lives) and any taxonomy terms.
///
/// We don't load the entire XML into memory — XDocument is OK at the ~439
/// property scale joseforland.com runs at, but the code is structured to
/// swap to XmlReader streaming if we ever migrate a 50k-listing site.
/// </summary>
public class WxrReader(string path)
{
    public IEnumerable<WpItem> ReadItems()
    {
        var doc = XDocument.Load(path);
        // WXR namespaces — values are the WXR 1.2 spec URIs.
        XNamespace content = "http://purl.org/rss/1.0/modules/content/";
        XNamespace wp      = "http://wordpress.org/export/1.2/";
        XNamespace dc      = "http://purl.org/dc/elements/1.1/";

        foreach (var item in doc.Descendants("item"))
        {
            int.TryParse((string?)item.Element(wp + "post_id"), out var postId);

            var rawDate = (string?)item.Element(wp + "post_date_gmt") ?? (string?)item.Element(wp + "post_date") ?? "";
            DateTime.TryParse(rawDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var date);

            yield return new WpItem
            {
                PostId    = postId,
                Title     = ((string?)item.Element("title") ?? "").Trim(),
                Link      = (string?)item.Element("link") ?? "",
                Creator   = (string?)item.Element(dc + "creator") ?? "",
                Content   = (string?)item.Element(content + "encoded") ?? "",
                PostType  = (string?)item.Element(wp + "post_type") ?? "",
                Status    = (string?)item.Element(wp + "status") ?? "",
                PostDate  = date,
                Meta = item.Elements(wp + "postmeta")
                    .Select(m => new KeyValuePair<string, string>(
                        (string?)m.Element(wp + "meta_key") ?? "",
                        (string?)m.Element(wp + "meta_value") ?? ""))
                    .Where(kv => !string.IsNullOrEmpty(kv.Key))
                    .ToList(),
                Categories = item.Elements("category")
                    .Select(c => new WpCategory {
                        Domain   = (string?)c.Attribute("domain") ?? "",
                        Nicename = (string?)c.Attribute("nicename") ?? "",
                        Label    = c.Value,
                    })
                    .ToList(),
            };
        }
    }
}

public class WpItem
{
    public int PostId { get; set; }
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Creator { get; set; } = "";
    public string Content { get; set; } = "";
    public string PostType { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime PostDate { get; set; }
    public List<KeyValuePair<string, string>> Meta { get; set; } = new();
    public List<WpCategory> Categories { get; set; } = new();

    /// <summary>Lookup helper — first matching meta_value or null.</summary>
    public string? GetMeta(params string[] keys)
    {
        foreach (var k in keys)
        {
            var hit = Meta.FirstOrDefault(m => m.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(hit.Value)) return hit.Value;
        }
        return null;
    }
}

public class WpCategory
{
    public string Domain { get; set; } = "";    // taxonomy name — e.g. "es_categories"
    public string Nicename { get; set; } = "";  // slug
    public string Label { get; set; } = "";     // display name
}

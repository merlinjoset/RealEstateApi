using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RealEstateApi.Data;
using RealEstateApi.Models;
using EstatikImporter;

// ── Args parsing ─────────────────────────────────────────────────────────────
// Two modes:
//   --analyze <path>   No DB writes. Dumps post-type breakdown, unique meta
//                       keys with sample values, taxonomy domains. Use this
//                       FIRST when a new WXR file arrives to understand what
//                       Estatik actually stored before writing the mapper.
//   --import  <path>   Writes to the DB. Idempotent — matches on WpPostId so
//                       reruns update existing rows instead of duplicating.
if (args.Length < 2 || (args[0] != "--analyze" && args[0] != "--import"))
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project Tools/EstatikImporter -- --analyze path/to/wp-export.xml");
    Console.Error.WriteLine("  dotnet run --project Tools/EstatikImporter -- --import  path/to/wp-export.xml");
    return 1;
}

var mode = args[0];
var inputPath = args[1];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"File not found: {inputPath}");
    return 1;
}

Console.WriteLine($"📄 Reading {inputPath}...");
var reader = new WxrReader(inputPath);

if (mode == "--analyze")
{
    return RunAnalyze(reader);
}

// ── Import mode ──────────────────────────────────────────────────────────────
// Pull connection string from the API's appsettings.json so we don't keep two
// copies of it. Falls back to the DOTNET_RUNNING_IN_CONTAINER conventional
// env var if you want to point at a different DB just for the import.
var config = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var conn = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("❌ ConnectionStrings:DefaultConnection is empty. Set it in appsettings.json or via env var.");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(conn)
    .Options;

await using var db = new AppDbContext(options);
return await RunImport(reader, db);


// ─────────────────── Analyze mode ───────────────────
static int RunAnalyze(WxrReader reader)
{
    var byType = new Dictionary<string, int>();
    var sampleMeta = new Dictionary<string, Dictionary<string, string>>(); // postType → metaKey → sampleValue
    var taxonomies = new Dictionary<string, HashSet<string>>();             // postType → set of taxonomy domains

    foreach (var item in reader.ReadItems())
    {
        byType[item.PostType] = byType.GetValueOrDefault(item.PostType) + 1;

        if (!sampleMeta.TryGetValue(item.PostType, out var meta))
            sampleMeta[item.PostType] = meta = new Dictionary<string, string>();
        foreach (var (k, v) in item.Meta)
            if (!meta.ContainsKey(k)) meta[k] = Truncate(v, 80);

        if (!taxonomies.TryGetValue(item.PostType, out var taxSet))
            taxonomies[item.PostType] = taxSet = new HashSet<string>();
        foreach (var c in item.Categories) taxSet.Add(c.Domain);
    }

    Console.WriteLine();
    Console.WriteLine("━━━ Post types ━━━");
    foreach (var (type, count) in byType.OrderByDescending(kv => kv.Value))
        Console.WriteLine($"  {type,-30} {count}");

    foreach (var (type, meta) in sampleMeta.OrderByDescending(kv => byType[kv.Key]))
    {
        if (meta.Count == 0) continue;
        Console.WriteLine();
        Console.WriteLine($"━━━ Meta keys for '{type}' ({meta.Count} unique) ━━━");
        foreach (var (k, v) in meta.OrderBy(kv => kv.Key))
            Console.WriteLine($"  {k,-40} {v}");
    }

    Console.WriteLine();
    Console.WriteLine("━━━ Taxonomies ━━━");
    foreach (var (type, taxSet) in taxonomies.Where(kv => kv.Value.Count > 0))
        Console.WriteLine($"  {type,-30} {string.Join(", ", taxSet)}");

    Console.WriteLine();
    Console.WriteLine("Done. Send the above output back so the mapper can be finalised.");
    return 0;
}

static string Truncate(string s, int max) =>
    string.IsNullOrEmpty(s) ? ""
    : s.Length <= max ? s.Replace("\n", " ").Replace("\r", "")
    : s[..max].Replace("\n", " ").Replace("\r", "") + "…";


// ─────────────────── Import mode ───────────────────
static async Task<int> RunImport(WxrReader reader, AppDbContext db)
{
    Console.WriteLine("⚠ Import mode — will INSERT or UPDATE rows by WpPostId.");
    Console.Write("Type 'yes' to continue: ");
    if (Console.ReadLine()?.Trim().ToLowerInvariant() != "yes") return 1;

    int created = 0, updated = 0, skipped = 0;

    foreach (var item in reader.ReadItems())
    {
        // Skip non-property items (pages, attachments, posts, etc.).
        // Estatik uses 'es_property' (or 'property' on some setups) — accept both.
        if (item.PostType is not ("es_property" or "property")) continue;

        // Map → Property. The mapper assumes Estatik's standard meta keys; if
        // your analyze run reveals different ones, update PropertyMapper first.
        var draft = PropertyMapper.FromWpItem(item);
        if (draft is null) { skipped++; continue; }

        var existing = await db.Properties.FirstOrDefaultAsync(p => p.WpPostId == item.PostId);
        if (existing is null)
        {
            db.Properties.Add(draft);
            created++;
        }
        else
        {
            // Update in place — preserves our internal Id so any inquiries
            // already pinned to it survive.
            PropertyMapper.CopyInto(draft, existing);
            updated++;
        }
    }

    Console.WriteLine($"💾 Saving... (created={created}, updated={updated}, skipped={skipped})");
    await db.SaveChangesAsync();
    Console.WriteLine("✓ Done.");
    return 0;
}

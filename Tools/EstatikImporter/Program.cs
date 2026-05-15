using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RealEstateApi.Data;
using RealEstateApi.Models;
using EstatikImporter;

// Args:
//   --analyze <path>         No DB writes. Dumps post-type / meta-key shape.
//   --import  <path>         INSERT-or-UPDATE properties by WpPostId.
//   --import  <path> --reset DELETE existing Properties + Testimonials first
//                            (clears seed dummy rows so the WP import lands
//                            on a clean table). Idempotent on its own —
//                            re-running --import without --reset just updates
//                            in place.
if (args.Length < 2 || (args[0] != "--analyze" && args[0] != "--import"))
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project Tools/EstatikImporter -- --analyze path/to/wp-export.xml");
    Console.Error.WriteLine("  dotnet run --project Tools/EstatikImporter -- --import  path/to/wp-export.xml [--reset]");
    return 1;
}

var mode = args[0];
var inputPath = args[1];
var reset = args.Contains("--reset");

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
var config = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var conn = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("❌ ConnectionStrings:DefaultConnection is empty. Set it in appsettings.Development.json or via the ConnectionStrings__DefaultConnection env var.");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(conn)
    .Options;

await using var db = new AppDbContext(options);
return await RunImport(reader, db, reset);


// ─────────────────── Analyze mode ───────────────────
static int RunAnalyze(WxrReader reader)
{
    var byType = new Dictionary<string, int>();
    var sampleMeta = new Dictionary<string, Dictionary<string, string>>();
    var taxonomies = new Dictionary<string, HashSet<string>>();

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
    return 0;
}

static string Truncate(string s, int max) =>
    string.IsNullOrEmpty(s) ? ""
    : s.Length <= max ? s.Replace("\n", " ").Replace("\r", "")
    : s[..max].Replace("\n", " ").Replace("\r", "") + "…";


// ─────────────────── Import mode ───────────────────
static async Task<int> RunImport(WxrReader reader, AppDbContext db, bool reset)
{
    // Make sure the schema has caught up before we touch the DB — the
    // importer adds the WpPostId column via the AddWpImportColumns
    // migration; running on an out-of-date DB would explode with a
    // "column does not exist" error mid-import.
    Console.WriteLine("Applying any pending EF migrations...");
    await db.Database.MigrateAsync();
    Console.WriteLine("  → schema is current");

    if (reset)
    {
        Console.WriteLine("⚠ --reset will DELETE every existing Property and Testimonial row.");
        Console.WriteLine("  Other tables (Users, SmsTemplates, SiteSettings) are left alone.");
    }
    Console.WriteLine("⚠ Import will INSERT or UPDATE properties by WpPostId.");
    Console.Write("Type 'yes' to continue: ");
    if (Console.ReadLine()?.Trim().ToLowerInvariant() != "yes") return 1;

    // ── Pass 1: build attachment-id → URL map. WordPress stores image
    //    URLs in attachment posts; properties reference them only by ID. ──
    Console.WriteLine("Pass 1/2: scanning attachments to build image map...");
    var attachments = new AttachmentMap("https://joseforland.com");
    foreach (var item in reader.ReadItems())
    {
        if (item.PostType != "attachment") continue;
        var rel = item.GetMeta("_wp_attached_file");
        if (!string.IsNullOrWhiteSpace(rel))
            attachments.Add(item.PostId, rel);
    }
    Console.WriteLine($"  → mapped {attachments.Count} attachments");

    // ── Optional reset: wipe seed Properties + Testimonials. We keep Users
    //    (so the admin can still sign in) and SmsTemplates / SiteSettings
    //    (configured infrastructure). ──
    if (reset)
    {
        Console.WriteLine("Resetting Properties + Testimonials tables...");
        // Raw SQL because EF's DbContext.RemoveRange + SaveChanges would
        // trip the soft-delete interceptor and just flip IsDeleted=true,
        // leaving the rows visible to the WpPostId match check.
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""SavedProperties"";
            DELETE FROM ""Properties"";
            DELETE FROM ""Testimonials"";
        ");
        Console.WriteLine("  → reset done");
    }

    // ── Pass 2: import properties ──
    Console.WriteLine("Pass 2/2: importing properties...");
    int created = 0, updated = 0, skipped = 0;
    foreach (var item in reader.ReadItems())
    {
        if (item.PostType != "properties") continue;

        var draft = PropertyMapper.FromWpItem(item, attachments);
        if (draft is null) { skipped++; continue; }

        var existing = await db.Properties.FirstOrDefaultAsync(p => p.WpPostId == item.PostId);
        if (existing is null)
        {
            db.Properties.Add(draft);
            created++;
        }
        else
        {
            PropertyMapper.CopyInto(draft, existing);
            updated++;
        }

        if ((created + updated) % 50 == 0 && (created + updated) > 0)
        {
            await db.SaveChangesAsync();
            Console.WriteLine($"  ✓ {created + updated} processed so far (saved batch)");
        }
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"✓ Done. created={created}, updated={updated}, skipped={skipped}");
    return 0;
}

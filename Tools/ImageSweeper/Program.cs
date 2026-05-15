using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RealEstateApi.Data;

// Sweep every Property.Images URL that still points at the legacy
// joseforland.com WordPress host: download the file to a local
// uploads/ folder, then rewrite each URL in the DB so the
// production API serves the image instead.
//
// Args:
//   --out <path>            Local folder to download into. Default: ./uploads
//   --new-base <url>        New URL prefix that replaces "https://joseforland.com/wp-content/uploads".
//                           Default: "https://api.joseforland.com/media"
//   --legacy-host <host>    Hostname considered "legacy" — only URLs at this host are swept.
//                           Default: "joseforland.com"
//   --dry-run               Don't write to the DB. Useful for a first pass.
//   --parallel <n>          Concurrent downloads. Default: 6.

var outDir       = ArgValue("--out", "uploads");
var newBase      = ArgValue("--new-base", "https://api.joseforland.com/media").TrimEnd('/');
var legacyHost   = ArgValue("--legacy-host", "joseforland.com");
var dryRun       = args.Contains("--dry-run");
var parallel     = int.TryParse(ArgValue("--parallel", "6"), out var p) ? p : 6;

Directory.CreateDirectory(outDir);

var config = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var conn = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("❌ ConnectionStrings:DefaultConnection is empty.");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
await using var db = new AppDbContext(options);

Console.WriteLine($"📥 Sweeping images from {legacyHost} → {outDir}");
Console.WriteLine($"   New base URL: {newBase}");
Console.WriteLine($"   {(dryRun ? "DRY RUN — no DB writes" : "Will UPDATE Property.Images in DB.")}");
Console.WriteLine();

var properties = await db.Properties
    .Where(p => p.Images.Any())
    .Select(p => new { p.Id, p.Images })
    .ToListAsync();

Console.WriteLine($"Properties with images: {properties.Count}");

// Build the full list of distinct URLs we need to fetch.
var distinctUrls = properties
    .SelectMany(p => p.Images)
    .Where(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                && uri.Host.Equals(legacyHost, StringComparison.OrdinalIgnoreCase))
    .Distinct()
    .ToList();

Console.WriteLine($"Unique URLs pointing at legacy host: {distinctUrls.Count}");
if (distinctUrls.Count == 0)
{
    Console.WriteLine("Nothing to do — every image is already on the new host. ✓");
    return 0;
}

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("JoseForLandImageSweeper/1.0");

int ok = 0, skipped = 0, failed = 0;
var failedUrls = new ConcurrentBag<string>();
var semaphore = new SemaphoreSlim(parallel);

var tasks = distinctUrls.Select(async url =>
{
    await semaphore.WaitAsync();
    try
    {
        var target = TargetPathFor(url, legacyHost, outDir);
        if (target is null)
        {
            Interlocked.Increment(ref skipped);
            return;
        }
        if (File.Exists(target))
        {
            Interlocked.Increment(ref skipped);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        try
        {
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ✗ {resp.StatusCode} — {url}");
                failedUrls.Add(url);
                Interlocked.Increment(ref failed);
                return;
            }
            await using var fs = File.OpenWrite(target);
            await resp.Content.CopyToAsync(fs);
            var localOk = Interlocked.Increment(ref ok);
            if (localOk % 25 == 0) Console.WriteLine($"  ✓ {localOk} downloaded so far");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ {ex.GetType().Name}: {ex.Message} — {url}");
            failedUrls.Add(url);
            Interlocked.Increment(ref failed);
        }
    }
    finally { semaphore.Release(); }
});

await Task.WhenAll(tasks);
Console.WriteLine();
Console.WriteLine($"Download summary: ok={ok}, skipped(existing)={skipped}, failed={failed}");

if (dryRun)
{
    Console.WriteLine("Dry-run — DB unchanged.");
    return 0;
}

// ── Rewrite Property.Images URLs ────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("Rewriting URLs in DB...");
int rewrittenProperties = 0;
var allProps = await db.Properties.Where(p => p.Images.Any()).ToListAsync();
foreach (var prop in allProps)
{
    var changed = false;
    var newImages = new List<string>();
    foreach (var url in prop.Images)
    {
        // Skip if not pointing at legacy host (already swept, or external).
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(legacyHost, StringComparison.OrdinalIgnoreCase))
        {
            newImages.Add(url);
            continue;
        }
        // Skip rewrite if the file didn't actually download — keep the
        // original URL so admin can investigate later.
        if (failedUrls.Contains(url))
        {
            newImages.Add(url);
            continue;
        }
        var newUrl = NewUrlFor(url, legacyHost, newBase);
        if (newUrl is null) { newImages.Add(url); continue; }
        newImages.Add(newUrl);
        changed = true;
    }
    if (changed)
    {
        prop.Images = newImages;
        rewrittenProperties++;
    }
}

await db.SaveChangesAsync();
Console.WriteLine($"✓ Done. Rewrote URLs on {rewrittenProperties} properties.");
return 0;


/// Compute the local target path for a downloaded URL. We mirror WordPress's
/// directory structure under `outDir`: wp-content/uploads/2023/04/x.jpg
/// becomes ./uploads/2023/04/x.jpg.
static string? TargetPathFor(string url, string legacyHost, string outDir)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
    if (!uri.Host.Equals(legacyHost, StringComparison.OrdinalIgnoreCase)) return null;

    // /wp-content/uploads/2023/04/x.jpg → 2023/04/x.jpg
    var path = uri.AbsolutePath.TrimStart('/');
    var marker = "wp-content/uploads/";
    var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    var rel = idx >= 0 ? path[(idx + marker.Length)..] : path;

    // Strip any leading slashes / Windows-illegal chars (Render is Linux but
    // the dev machine is Windows; keep both compatible).
    rel = rel.Replace('\\', '/').TrimStart('/');
    if (string.IsNullOrWhiteSpace(rel)) return null;

    return Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
}

static string? NewUrlFor(string url, string legacyHost, string newBase)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
    if (!uri.Host.Equals(legacyHost, StringComparison.OrdinalIgnoreCase)) return null;

    var path = uri.AbsolutePath.TrimStart('/');
    var marker = "wp-content/uploads/";
    var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    var rel = idx >= 0 ? path[(idx + marker.Length)..] : path;
    return $"{newBase}/{rel.TrimStart('/')}";
}

string ArgValue(string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return fallback;
}

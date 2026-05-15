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
//
//   --pull-relative <base>  Re-download every relative Property.Images URL (eg /media/2025/01/x.jpg)
//                           from <base>/wp-content/uploads/<rest> into <outDir>/<rest>. Run this on
//                           Render after deploying — it populates the persistent disk so the API's
//                           UseStaticFiles middleware can serve the files. Does NOT touch the DB.

var outDir       = ArgValue("--out", "uploads");
var newBase      = ArgValue("--new-base", "https://api.joseforland.com/media").TrimEnd('/');
var legacyHost   = ArgValue("--legacy-host", "joseforland.com");
var dryRun       = args.Contains("--dry-run");
var rewriteOnly  = args.Contains("--rewrite-only");
var listOnly     = args.Contains("--list");
var pullRelative = ArgValue("--pull-relative", "").TrimEnd('/');
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

// Inspection mode — just print a sample of the URLs currently stored so we
// can confirm what host/path the DB is pointing at.
if (listOnly)
{
    var bySchemeHost = properties
        .SelectMany(p => p.Images)
        .GroupBy(u =>
        {
            if (Uri.TryCreate(u, UriKind.Absolute, out var ab)) return $"{ab.Scheme}://{ab.Host}";
            if (u.StartsWith("/")) return "(relative)";
            return "(other)";
        })
        .Select(g => new { Bucket = g.Key, Count = g.Count(), Sample = g.First() })
        .OrderByDescending(g => g.Count)
        .ToList();
    Console.WriteLine("URL buckets:");
    foreach (var b in bySchemeHost)
        Console.WriteLine($"  {b.Count,6}  {b.Bucket}    e.g. {b.Sample}");
    return 0;
}

// ── --pull-relative mode ────────────────────────────────────────────────────
// Re-download every relative /media/* URL from <pullRelative>/wp-content/uploads/*
// to <outDir>. Used after deploying to Render — populates the persistent disk
// so UseStaticFiles can serve the files. Does NOT modify the DB.
if (!string.IsNullOrEmpty(pullRelative))
{
    Console.WriteLine($"Pull-relative mode — source: {pullRelative}/wp-content/uploads/...");
    Console.WriteLine($"   Saving to: {Path.GetFullPath(outDir)}");

    // Pull every distinct relative URL beginning with /media/ — that's the
    // canonical shape after the last rewrite-only sweep.
    var relPaths = properties
        .SelectMany(p => p.Images)
        .Where(u => u.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
        .Select(u => u["/media/".Length..])       // "2025/01/foo.jpg"
        .Distinct()
        .ToList();

    Console.WriteLine($"Unique /media paths to fetch: {relPaths.Count}");
    if (relPaths.Count == 0)
    {
        Console.WriteLine("Nothing to do — no relative /media URLs in DB.");
        return 0;
    }

    var pullHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    pullHttp.DefaultRequestHeaders.UserAgent.ParseAdd("JoseForLandImageSweeper/1.0");

    int pOk = 0, pSkip = 0, pFail = 0;
    var pSem = new SemaphoreSlim(parallel);
    var pTasks = relPaths.Select(async rel =>
    {
        await pSem.WaitAsync();
        try
        {
            var target = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(target)) { Interlocked.Increment(ref pSkip); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var sourceUrl = $"{pullRelative}/wp-content/uploads/{rel}";
            try
            {
                using var resp = await pullHttp.GetAsync(sourceUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  ✗ {resp.StatusCode} — {sourceUrl}");
                    Interlocked.Increment(ref pFail);
                    return;
                }
                await using var fs = File.OpenWrite(target);
                await resp.Content.CopyToAsync(fs);
                var n = Interlocked.Increment(ref pOk);
                if (n % 25 == 0) Console.WriteLine($"  ✓ {n} downloaded so far");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {ex.GetType().Name}: {ex.Message} — {sourceUrl}");
                Interlocked.Increment(ref pFail);
            }
        }
        finally { pSem.Release(); }
    });
    await Task.WhenAll(pTasks);
    Console.WriteLine();
    Console.WriteLine($"Pull summary: ok={pOk}, skipped(existing)={pSkip}, failed={pFail}");
    return pFail == 0 ? 0 : 2;
}

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

// In --rewrite-only mode we skip the network entirely and just rewrite the
// URLs in the DB. Useful when files are already on disk and we just need to
// change the public URL prefix (e.g. switching from absolute to relative).
if (rewriteOnly)
{
    Console.WriteLine($"Rewrite-only mode — replacing https://{legacyHost}/... → {newBase}/... in Property.Images");
    var rewriteProps = await db.Properties.Where(p => p.Images.Any()).ToListAsync();
    int touched = 0;
    foreach (var prop in rewriteProps)
    {
        var changed = false;
        var newImages = new List<string>();
        foreach (var url in prop.Images)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Host.Equals(legacyHost, StringComparison.OrdinalIgnoreCase))
            {
                // Strip the legacy origin AND the /media or /wp-content/uploads/ prefix
                // and reattach against the new base.
                var path = uri.AbsolutePath;
                var mediaIdx = path.IndexOf("/media/", StringComparison.OrdinalIgnoreCase);
                var wpIdx    = path.IndexOf("wp-content/uploads/", StringComparison.OrdinalIgnoreCase);
                string rel;
                if (mediaIdx >= 0)      rel = path[(mediaIdx + "/media/".Length)..];
                else if (wpIdx >= 0)    rel = path[(wpIdx + "wp-content/uploads/".Length)..];
                else                    rel = path.TrimStart('/');
                newImages.Add($"{newBase}/{rel.TrimStart('/')}");
                changed = true;
            }
            else
            {
                newImages.Add(url);
            }
        }
        if (changed) { prop.Images = newImages; touched++; }
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"✓ Rewrote URLs on {touched} properties.");
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

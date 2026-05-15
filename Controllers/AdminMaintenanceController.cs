using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;

namespace RealEstateApi.Controllers;

/// <summary>
/// One-shot maintenance endpoints. Currently used for seeding the
/// /var/data/uploads disk on Render from the legacy WordPress host —
/// the ImageSweeper CLI tool needs the .NET SDK at runtime, which the
/// Render runtime image doesn't ship, so we trigger the same logic
/// in-process via an HTTP call instead.
/// </summary>
[ApiController]
[Route("api/admin/maintenance")]
[Authorize(Roles = "Admin")]
public class AdminMaintenanceController(
    AppDbContext db,
    IConfiguration config,
    IWebHostEnvironment env,
    IHttpClientFactory httpFactory,
    ILogger<AdminMaintenanceController> log) : ControllerBase
{
    /// <summary>
    /// Re-download every relative /media/* URL in Property.Images from
    /// {source}/wp-content/uploads/* into the configured Storage:UploadsPath
    /// directory. Idempotent — files that already exist on disk are skipped.
    ///
    /// Returns a JSON summary so the caller can verify the result without
    /// scraping logs. Bounded to 10 concurrent fetches to stay polite to
    /// the source host.
    /// </summary>
    /// <param name="source">Base URL of the legacy WP install. Defaults to https://joseforland.com.</param>
    [HttpPost("seed-media-from-legacy")]
    public async Task<IActionResult> SeedMediaFromLegacy([FromQuery] string source = "https://joseforland.com")
    {
        source = source.TrimEnd('/');

        var rawPath = config["Storage:UploadsPath"] ?? "uploads";
        var uploadsPath = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(env.ContentRootPath, rawPath);
        Directory.CreateDirectory(uploadsPath);

        // Pull every distinct relative path starting with /media/.
        var properties = await db.Properties
            .Where(p => p.Images.Any())
            .Select(p => p.Images)
            .ToListAsync();

        var relPaths = properties
            .SelectMany(arr => arr)
            .Where(u => u.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            .Select(u => u["/media/".Length..])
            .Distinct()
            .ToList();

        if (relPaths.Count == 0)
            return Ok(new { ok = 0, skipped = 0, failed = 0, total = 0, uploadsPath, message = "No relative /media URLs in DB." });

        var http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("JoseForLandSeed/1.0");

        int ok = 0, skipped = 0, failed = 0;
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var sem = new SemaphoreSlim(10);

        var tasks = relPaths.Select(async rel =>
        {
            await sem.WaitAsync();
            try
            {
                var target = Path.Combine(uploadsPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(target)) { Interlocked.Increment(ref skipped); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var sourceUrl = $"{source}/wp-content/uploads/{rel}";
                try
                {
                    using var resp = await http.GetAsync(sourceUrl);
                    if (!resp.IsSuccessStatusCode)
                    {
                        failures.Add($"{(int)resp.StatusCode} {rel}");
                        Interlocked.Increment(ref failed);
                        return;
                    }
                    await using var fs = System.IO.File.OpenWrite(target);
                    await resp.Content.CopyToAsync(fs);
                    Interlocked.Increment(ref ok);
                }
                catch (Exception ex)
                {
                    failures.Add($"{ex.GetType().Name} {rel}");
                    Interlocked.Increment(ref failed);
                }
            }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks);

        log.LogInformation("Media seed complete: ok={Ok} skipped={Skipped} failed={Failed} total={Total}",
            ok, skipped, failed, relPaths.Count);

        return Ok(new
        {
            ok,
            skipped,
            failed,
            total = relPaths.Count,
            uploadsPath,
            failures = failures.Take(20).ToList(),  // cap so the response stays small
        });
    }
}

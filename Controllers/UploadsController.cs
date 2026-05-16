using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers;

/// <summary>
/// File upload endpoints. Currently handles property image uploads only,
/// writing into the configured Storage:UploadsPath disk that the static
/// file middleware serves at /media/*.
///
/// Anonymous access is allowed because the public /sell submission flow
/// needs to upload before the seller has an account. We mitigate abuse
/// with strict per-file content-type, extension, and size checks.
/// </summary>
[ApiController]
[Route("api/uploads")]
public class UploadsController(
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<UploadsController> log) : ControllerBase
{
    // Keep these conservative — images only, small enough that a phone
    // photo lands without a long upload but a malicious 50 MB blob can't.
    private const long MaxBytes = 5 * 1024 * 1024;       // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
    };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    /// <summary>
    /// Upload a single property image. Returns the public URL (relative
    /// path under /media) so the caller can drop it straight into the
    /// Property.Images array on the create / edit request.
    /// </summary>
    [HttpPost("property-image")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxBytes + 1024)]   // small headroom for multipart overhead
    public async Task<IActionResult> UploadPropertyImage([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file received." });

        if (file.Length > MaxBytes)
            return BadRequest(new { error = $"File too large. Max size is {MaxBytes / 1024 / 1024} MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}." });

        if (!AllowedContentTypes.Contains(file.ContentType ?? ""))
            return BadRequest(new { error = $"Unsupported content type '{file.ContentType}'." });

        // Land the file under uploads/YYYY/MM/ to mirror the WordPress
        // layout we already use for legacy images. The filename gets a
        // GUID prefix to avoid collisions and to defeat any path traversal
        // hidden in user-supplied names.
        var rawPath = config["Storage:UploadsPath"] ?? "uploads";
        var uploadsRoot = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(env.ContentRootPath, rawPath);

        var now = DateTime.UtcNow;
        var relDir = $"{now:yyyy}/{now:MM}";
        var diskDir = Path.Combine(uploadsRoot, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(diskDir);

        // Sanitise the basename — strip path separators and any non-safe chars.
        var basename = Path.GetFileNameWithoutExtension(file.FileName);
        var safe = new string(basename
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(safe)) safe = "image";
        if (safe.Length > 60) safe = safe[..60];

        var unique = $"{Guid.NewGuid():N}-{safe}{ext}";
        var diskPath = Path.Combine(diskDir, unique);

        await using (var fs = System.IO.File.Create(diskPath))
            await file.CopyToAsync(fs);

        var publicUrl = $"/media/{relDir}/{unique}";
        log.LogInformation("Uploaded property image: {Url} ({Bytes} bytes)", publicUrl, file.Length);

        return Ok(new { url = publicUrl });
    }
}

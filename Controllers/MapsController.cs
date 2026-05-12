using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers;

/// <summary>
/// Helpers for working with map URLs. The main use-case is resolving Google
/// Maps short links (e.g. https://maps.app.goo.gl/xyz), which the browser
/// can't follow on its own due to CORS — they redirect to maps.google.com
/// with the actual coordinates in the long URL.
/// </summary>
[ApiController]
[Route("api/maps")]
public class MapsController(ILogger<MapsController> logger) : ControllerBase
{
    public record ResolveRequest(string Url);
    public record ResolveResponse(double Latitude, double Longitude, string ResolvedUrl);

    /// <summary>
    /// Follow redirects on a Google Maps URL and extract lat/lng from the
    /// final canonical URL. Works on short links (maps.app.goo.gl, goo.gl/maps)
    /// as well as on regular long Google Maps URLs.
    /// </summary>
    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            return BadRequest(new { error = "URL is required." });

        if (!Uri.TryCreate(req.Url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            return BadRequest(new { error = "Please paste a full https:// URL." });

        // Only follow redirects on Google-owned hosts. Refusing arbitrary
        // hosts prevents this endpoint from being abused as an open redirect
        // probe / SSRF vector.
        var allowedHosts = new[]
        {
            "maps.app.goo.gl", "goo.gl", "g.co", "maps.google.com",
            "www.google.com", "google.com",
        };
        if (!allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only Google Maps URLs can be resolved." });

        // First, try to parse the URL as-is (already a long URL).
        if (TryExtractCoords(uri.AbsoluteUri, out var lat, out var lng))
            return Ok(new ResolveResponse(lat, lng, uri.AbsoluteUri));

        // Otherwise, follow redirects manually so we can capture the final URL
        // even if the body is JS-only.
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; JoseForLandBot/1.0; +https://joseforland.com)");

        var currentUri = uri;
        try
        {
            for (var i = 0; i < 8; i++) // bounded redirect chain
            {
                ct.ThrowIfCancellationRequested();
                using var resp = await http.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, ct);

                // Try the current URL on every hop — Google sometimes 200s with
                // the coords baked into the canonical URL.
                if (TryExtractCoords(currentUri.AbsoluteUri, out lat, out lng))
                    return Ok(new ResolveResponse(lat, lng, currentUri.AbsoluteUri));

                if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 && resp.Headers.Location is { } next)
                {
                    currentUri = next.IsAbsoluteUri ? next : new Uri(currentUri, next);
                    continue;
                }

                // Last resort: scan the HTML body for an @lat,lng marker
                // (Google sometimes embeds it as JSON in noscript).
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    if (TryExtractCoords(body, out lat, out lng))
                        return Ok(new ResolveResponse(lat, lng, currentUri.AbsoluteUri));
                }
                break;
            }
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = "Google Maps did not respond in time. Please try again." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to follow Google Maps redirect for {Url}", req.Url);
            return BadRequest(new { error = "Could not resolve that URL. Please paste the long URL from your browser bar." });
        }

        return BadRequest(new
        {
            error = "Could not find coordinates in that URL. Open the link in Google Maps, then copy the long URL from your browser address bar.",
        });
    }

    /// <summary>
    /// Pull lat/lng out of any Google Maps URL/body shape we've seen:
    ///   /@lat,lng,zoom
    ///   ?q=lat,lng    ?ll=lat,lng    ?destination=lat,lng
    ///   bare "lat,lng"
    ///   !3dLAT!4dLNG  (data parameter, often present in shared links)
    /// </summary>
    private static bool TryExtractCoords(string s, out double lat, out double lng)
    {
        lat = 0; lng = 0;

        // /@lat,lng[,zoom]
        var at = Regex.Match(s, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
        if (at.Success
            && double.TryParse(at.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(at.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out lng))
            return true;

        // ?q=lat,lng / ?ll= / ?destination= / ?query=
        var q = Regex.Match(s, @"[?&](?:q|ll|query|destination)=(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)");
        if (q.Success
            && double.TryParse(q.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(q.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out lng))
            return true;

        // !3d{lat}!4d{lng}  (Google's "data" param shape)
        var data = Regex.Match(s, @"!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)");
        if (data.Success
            && double.TryParse(data.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(data.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out lng))
            return true;

        // bare "lat,lng" — only treat as a full match (not embedded), so we
        // don't accidentally match arbitrary digit pairs in HTML.
        var bare = Regex.Match(s.Trim(), @"^\s*(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)\s*$");
        if (bare.Success
            && double.TryParse(bare.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(bare.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out lng))
            return true;

        return false;
    }
}

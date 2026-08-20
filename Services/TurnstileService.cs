using System.Text.Json;

namespace RealEstateApi.Services;

public interface ITurnstileService
{
    /// <summary>
    /// Verify a Cloudflare Turnstile token. Returns true when the token is
    /// valid — OR when Turnstile isn't configured (no secret), so local dev
    /// and un-provisioned environments keep working. Returns false only when
    /// a secret IS configured and the token is missing/invalid.
    /// </summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default);
}

/// <summary>
/// Server-side verification for Cloudflare Turnstile (the CAPTCHA on the
/// public register + inquiry forms). Fail-open when unconfigured so the app
/// runs without keys; fail-closed once <c>Turnstile:Secret</c> is set.
///
/// Config: Turnstile:Secret  (env var Turnstile__Secret on Render)
/// </summary>
public class TurnstileService(
    IConfiguration cfg,
    IHttpClientFactory httpFactory,
    ILogger<TurnstileService> log) : ITurnstileService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
    {
        var secret = cfg["Turnstile:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
            return true; // not configured → skip verification (dev / un-provisioned)

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("secret", secret),
                new("response", token!),
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                form.Add(new("remoteip", remoteIp!));

            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var resp = await client.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var ok = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!ok) log.LogWarning("🛡  Turnstile verification failed: {Json}", json);
            return ok;
        }
        catch (Exception ex)
        {
            // Network hiccup reaching Cloudflare — don't hard-block a real user
            // over an outage. Log and let it through.
            log.LogWarning(ex, "🛡  Turnstile verify errored — allowing request through");
            return true;
        }
    }
}

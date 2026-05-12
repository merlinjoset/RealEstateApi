using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealEstateApi.Services;

/// <summary>
/// Sends WhatsApp messages directly via the Meta WhatsApp Cloud API.
///
/// Free tier:
///   - 1,000 service conversations per month at no cost
///   - Each "service conversation" lasts 24 h from the user's first message
///   - After that, ~₹0.30 / message in India
///
/// What this adapter does today:
///   - Sends *free-form text* messages (the "type:text" Cloud API payload)
///   - This works in the customer-service window: 24 hours after the
///     recipient sent us ANY message (e.g. just after they submit an inquiry)
///   - Outside that window, Meta requires a pre-approved *template message*;
///     the request fails with error code 131047 / 131051 and we return false
///     so the NotificationService falls back to plain SMS automatically
///
/// What it does NOT do yet:
///   - Doesn't send template messages — those need the template name + an
///     ordered list of parameters. When the user has at least one approved
///     template, add a SendTemplateAsync() overload that calls:
///       POST /{phone-number-id}/messages
///       { "messaging_product":"whatsapp", "to":"...", "type":"template",
///         "template": { "name":"...", "language":{"code":"en_US"},
///                       "components":[{ "type":"body", "parameters":[...]}] } }
///
/// Required config keys (set on Render):
///   WhatsApp__Meta__PhoneNumberId  — the numeric ID, NOT the phone number itself
///   WhatsApp__Meta__AccessToken    — a long-lived "System User" token, not the
///                                    short-lived test token from the Cloud API page
///   WhatsApp__Meta__ApiVersion     — optional, defaults to "v19.0"
///
/// Setup walkthrough:
///   https://developers.facebook.com/docs/whatsapp/cloud-api/get-started
/// </summary>
public class MetaCloudWhatsAppService(
    HttpClient http,
    IConfiguration config,
    ILogger<MetaCloudWhatsAppService> log) : IWhatsAppService
{
    private string PhoneNumberId => config["WhatsApp:Meta:PhoneNumberId"] ?? "";
    private string AccessToken   => config["WhatsApp:Meta:AccessToken"] ?? "";
    private string ApiVersion    => config["WhatsApp:Meta:ApiVersion"] ?? "v19.0";

    public async Task<bool> TrySendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumberId) || string.IsNullOrWhiteSpace(AccessToken))
        {
            log.LogWarning("💬 [WhatsApp skipped — Meta credentials not set] {Phone}", toPhone);
            return false;
        }

        var normalized = NormalizeIndianPhone(toPhone);
        if (normalized is null)
        {
            log.LogWarning("💬 [WhatsApp skipped — invalid Indian mobile] {Phone}", toPhone);
            return false;
        }

        // Cloud API expects the number with country code, no '+' or spaces.
        var payload = new
        {
            messaging_product = "whatsapp",
            to = $"91{normalized}",
            type = "text",
            text = new { body = message, preview_url = false },
        };

        var url = $"https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}/messages";
        string rawBody = "(no body)";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            req.Content = JsonContent.Create(payload);

            using var res = await http.SendAsync(req, ct);
            rawBody = await res.Content.ReadAsStringAsync(ct);

            if (res.IsSuccessStatusCode)
            {
                // Successful response shape:
                // { "messaging_product":"whatsapp", "contacts":[{...}], "messages":[{"id":"wamid.HBgL..."}] }
                MetaSuccess? ok = null;
                try { ok = JsonSerializer.Deserialize<MetaSuccess>(rawBody); }
                catch (JsonException) { /* keep going; rawBody is logged below */ }

                log.LogInformation("💬 [WhatsApp sent via Meta Cloud] {Phone} (wamid {Wamid})",
                    normalized, ok?.Messages?.FirstOrDefault()?.Id ?? "(unknown)");
                return true;
            }

            // Failure — try to parse the structured error so we can log
            // something meaningful, but log the raw body either way.
            MetaError? err = null;
            try { err = JsonSerializer.Deserialize<MetaError>(rawBody); }
            catch (JsonException) { /* keep going */ }

            var code = err?.Error?.Code;
            var msg  = err?.Error?.Message ?? "(no message)";

            // Codes 131047 / 131051 / 131026 = recipient outside 24h window
            // or hasn't initiated a conversation — fall through to SMS.
            // We log at Info level for these "expected" cases, Warning for real errors.
            if (code is 131047 or 131051 or 131026)
            {
                log.LogInformation(
                    "↪ [WhatsApp not deliverable for {Phone}] outside 24h window / no opt-in (code {Code}) — falling back to SMS",
                    normalized, code);
            }
            else
            {
                log.LogWarning(
                    "💬 [WhatsApp failed via Meta Cloud] {Phone} ({Status}, code {Code}) — {Msg} — raw: {Body}",
                    normalized, (int)res.StatusCode, code, msg, Truncate(rawBody, 500));
            }
            return false;
        }
        catch (Exception ex)
        {
            log.LogError(ex,
                "💬 [WhatsApp error via Meta Cloud] {Phone} — raw response (if any): {Body}",
                normalized, Truncate(rawBody, 500));
            return false;
        }
    }

    /// <summary>10-digit Indian mobile, or null if it doesn't match.</summary>
    private static string? NormalizeIndianPhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("91") && digits.Length == 12) digits = digits[2..];
        if (digits.StartsWith("0")  && digits.Length == 11) digits = digits[1..];
        return digits.Length == 10 ? digits : null;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "(empty)"
        : s.Length <= max ? s
        : s[..max] + "…";

    // ── Meta Cloud API response shapes ───────────────────────────────────
    private record MetaSuccess(
        [property: JsonPropertyName("messages")] List<MetaMessageRef>? Messages);
    private record MetaMessageRef([property: JsonPropertyName("id")] string Id);

    private record MetaError([property: JsonPropertyName("error")] MetaErrorDetail? Error);
    private record MetaErrorDetail(
        [property: JsonPropertyName("code")]    int?    Code,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("type")]    string? Type);
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

/// <summary>
/// Delivers SMS to real Indian mobile inboxes via Fast2SMS — free tier
/// requires NO DLT registration, NO sender ID, NO template approval.
///
/// Quick setup:
///   1. Sign up at https://www.fast2sms.com  (free — gets you ~50 free SMS credits)
///   2. Verify your phone number
///   3. Dashboard → "Dev API" → copy the Authorization Key
///   4. Drop it into appsettings.Development.json:
///
///      "Sms": {
///        "Provider": "Fast2Sms",
///        "Fast2Sms": {
///          "AuthKey": "YOUR_FAST2SMS_AUTH_KEY"
///        }
///      }
///
/// Pricing: ~₹0.18–0.25 per SMS for additional credits beyond the free tier.
/// Goes to the recipient's regular SMS inbox (not WhatsApp).
/// API docs: https://docs.fast2sms.com
/// </summary>
public class Fast2SmsService(
    HttpClient http,
    IConfiguration config,
    AppDbContext db,
    ILogger<Fast2SmsService> log) : ISmsService
{
    private string AuthKey => config["Sms:Fast2Sms:AuthKey"] ?? "";

    /// <summary>
    /// Which Fast2SMS delivery route to use. Dashboard menu name → API value:
    ///
    ///   "q"    — "Quick SMS" tab. International gateway, manually approved,
    ///            ~₹5/SMS. THIS IS THE DEFAULT — it's the only route the
    ///            account is provisioned for without extra setup. ⚠ Fast2SMS
    ///            still attempts DND filtering on this route — numbers on
    ///            the TRAI DND registry may not receive the SMS even though
    ///            the API returns 200 + a request ID. Check Fast2SMS'
    ///            "Delivery Reports" page for the actual delivery state.
    ///   "otp"  — "OTP Message" tab. ~₹0.25/SMS, requires Fast2SMS website
    ///            verification (TXT record / file upload to your domain).
    ///   "dlt"  — "DLT SMS" tab. ~₹0.25/SMS, requires full TRAI DLT
    ///            registration of sender ID + templates (1-2 wk process).
    ///            ONLY route that guarantees delivery to DND numbers.
    ///   "dlt_manual" — DLT but templates submitted manually per send.
    ///   "p" / "v3" — Deprecated. Return status_code 990 "old API".
    ///
    /// Override on Render via env var:  Sms__Fast2Sms__Route
    /// </summary>
    private string Route => config["Sms:Fast2Sms:Route"] ?? "q";

    public async Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(AuthKey))
        {
            log.LogWarning("📱 [SMS skipped — Fast2Sms:AuthKey not set] {Phone}: {Message}", toPhone, message);
            return;
        }

        var normalized = NormalizeIndianPhone(toPhone);
        if (normalized is null)
        {
            log.LogWarning("📱 [SMS skipped — invalid Indian mobile] {Phone}", toPhone);
            return;
        }

        // Endpoint: https://www.fast2sms.com/dev/bulkV2
        // Route is configurable — defaults to "otp" so we bypass TRAI DND and
        // every Indian mobile receives the message. Flip Sms__Fast2Sms__Route
        // to "q" if you only need promotional sends to non-DND numbers.
        var payload = new
        {
            route = Route,
            message,
            language = "english",
            flash = 0,
            numbers = normalized,
        };

        // Read the response body as a raw string FIRST so we can always log
        // exactly what Fast2SMS sent back — even when it's an HTML error
        // page or a JSON shape we don't expect (e.g. "message" as string vs.
        // List<string>, which differs between auth-fail vs. success responses).
        string rawBody = "(no body)";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.fast2sms.com/dev/bulkV2");
            req.Headers.Add("authorization", AuthKey);
            req.Content = JsonContent.Create(payload);

            using var res = await http.SendAsync(req, ct);
            rawBody = await res.Content.ReadAsStringAsync(ct);

            // Best-effort deserialize — never let a parse error become the
            // visible failure mode, since the raw body is already logged.
            Fast2SmsResponse? body = null;
            try { body = JsonSerializer.Deserialize<Fast2SmsResponse>(rawBody); }
            catch (JsonException) { /* leave body null; rawBody covers it */ }

            if (res.IsSuccessStatusCode && body?.Return == true)
            {
                log.LogInformation(
                    "📱 [SMS sent via Fast2SMS · route={Route}] {Phone} (req {RequestId})",
                    Route, normalized, body.RequestId);
            }
            else
            {
                log.LogError(
                    "📱 [SMS failed via Fast2SMS · route={Route}] {Phone} ({Status}) — response: {Body}",
                    Route, normalized, (int)res.StatusCode, Truncate(rawBody, 500));
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex,
                "📱 [SMS error via Fast2SMS · route={Route}] {Phone} — raw response (if any): {Body}",
                Route, normalized, Truncate(rawBody, 500));
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "(empty)"
        : s.Length <= max ? s
        : s[..max] + "…";

    public async Task NotifyAdminsAsync(string message, CancellationToken ct = default)
    {
        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Phone != null)
            .Select(u => u.Phone!)
            .ToListAsync(ct);

        var businessNumber = config["Sms:BusinessNumber"];
        if (!string.IsNullOrWhiteSpace(businessNumber)) admins.Add(businessNumber);

        foreach (var phone in admins.Distinct())
            await SendAsync(phone, message, ct);
    }

    /// <summary>
    /// Returns the bare 10-digit Indian mobile number, or null if it isn't one.
    /// Strips +91, leading 0, spaces, hyphens, parentheses.
    /// </summary>
    private static string? NormalizeIndianPhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("91") && digits.Length == 12) digits = digits[2..];
        if (digits.StartsWith("0")  && digits.Length == 11) digits = digits[1..];
        return digits.Length == 10 ? digits : null;
    }

    /// <summary>Fast2SMS API response shape.</summary>
    private record Fast2SmsResponse(
        [property: JsonPropertyName("return")] bool Return,
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("message")] List<string>? Messages
    )
    {
        // Computed helper — NOT a JSON-serialized property. Without JsonIgnore
        // the global camelCase naming policy would also map this to "message"
        // and collide with `Messages`, which throws at deserialization time.
        [JsonIgnore]
        public string Message => Messages is { Count: > 0 } ? string.Join(", ", Messages) : "";
    }
}

using System.Net.Http.Json;
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

        // Quick SMS route — promotional, no DLT requirement.
        // Endpoint: https://www.fast2sms.com/dev/bulkV2
        var payload = new
        {
            route = "q",          // "q" = Quick (no DLT). Use "dlt" for DLT-registered.
            message,
            language = "english",
            flash = 0,
            numbers = normalized,
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.fast2sms.com/dev/bulkV2");
            req.Headers.Add("authorization", AuthKey);
            req.Content = JsonContent.Create(payload);

            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<Fast2SmsResponse>(cancellationToken: ct);

            if (res.IsSuccessStatusCode && body?.Return == true)
            {
                log.LogInformation(
                    "📱 [SMS sent via Fast2SMS] {Phone} (req {RequestId}): {Message}",
                    normalized, body.RequestId, message);
            }
            else
            {
                log.LogError(
                    "📱 [SMS failed via Fast2SMS] {Phone} ({Status}): {Message} — response: {Response}",
                    normalized, res.StatusCode, body?.Message ?? "(no message)", body);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "📱 [SMS error via Fast2SMS] {Phone}", normalized);
        }
    }

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
        public string Message => Messages is { Count: > 0 } ? string.Join(", ", Messages) : "";
    }
}

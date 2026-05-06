using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface IWhatsAppService
{
    /// <summary>
    /// Try to deliver a WhatsApp message. Returns true if delivered,
    /// false if the number isn't on WhatsApp / delivery failed (caller should
    /// fall back to SMS in that case).
    /// </summary>
    Task<bool> TrySendAsync(string toPhone, string message, CancellationToken ct = default);
}

/// <summary>
/// Console-logging WhatsApp stub for development.
/// Swap with TwilioWhatsAppService / GupshupWhatsAppService in production.
///
/// Simulated behaviour: numbers ending in 0–6 are treated as having
/// WhatsApp; 7–9 are treated as not having it (so SMS fallback fires).
/// This lets you verify the routing logic end-to-end without real keys.
/// </summary>
public class ConsoleWhatsAppService(ILogger<ConsoleWhatsAppService> log) : IWhatsAppService
{
    public Task<bool> TrySendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        var lastDigit = toPhone.Where(char.IsDigit).LastOrDefault();
        var hasWhatsApp = lastDigit is >= '0' and <= '6';

        if (hasWhatsApp)
        {
            log.LogInformation("💬 [WhatsApp to {Phone}] {Message}", toPhone, message);
            return Task.FromResult(true);
        }

        log.LogInformation("💬 [WhatsApp NOT registered for {Phone}] — caller should fall back to SMS", toPhone);
        return Task.FromResult(false);
    }
}

/* ─────────────────────────────────────────────────────────────────────────
 * Production WhatsApp providers (popular in India):
 *
 * 1. Twilio WhatsApp Business
 *    - https://www.twilio.com/whatsapp
 *    - Approved templates required, ~$0.005-0.06 per conversation
 *
 * 2. Gupshup
 *    - https://www.gupshup.io
 *    - India-focused, faster onboarding, ₹0.30-0.50 per session message
 *
 * 3. Interakt
 *    - https://www.interakt.shop
 *    - Built on top of Meta Cloud API, popular for Indian SMBs
 *
 * Sample Twilio implementation:
 *
 *   public class TwilioWhatsAppService(IConfiguration cfg, ILogger<...> log) : IWhatsAppService {
 *     public async Task<bool> TrySendAsync(string to, string body, CancellationToken ct = default) {
 *       try {
 *         TwilioClient.Init(cfg["WhatsApp:Twilio:AccountSid"], cfg["WhatsApp:Twilio:AuthToken"]);
 *         var msg = await MessageResource.CreateAsync(
 *           body: body,
 *           from: new PhoneNumber($"whatsapp:{cfg["WhatsApp:Twilio:FromNumber"]}"),
 *           to: new PhoneNumber($"whatsapp:{to}"));
 *         return msg.ErrorCode is null;  // false → number not on WhatsApp → SMS fallback
 *       } catch { return false; }
 *     }
 *   }
 * ───────────────────────────────────────────────────────────────────────── */

/// <summary>
/// Smart router: tries WhatsApp first when the recipient prefers it,
/// falls back to plain SMS automatically when WhatsApp delivery fails
/// (e.g. the number isn't on WhatsApp).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send a message to a single recipient. When <paramref name="preferWhatsApp"/>
    /// is true, attempts WhatsApp first and falls back to SMS on failure.
    /// </summary>
    Task SendAsync(string toPhone, string message, bool preferWhatsApp = false, CancellationToken ct = default);

    /// <summary>Broadcast to every active admin via SMS (and WhatsApp if available).</summary>
    Task NotifyAdminsAsync(string message, CancellationToken ct = default);
}

public class NotificationService(
    IWhatsAppService whatsApp,
    ISmsService sms,
    AppDbContext db,
    ILogger<NotificationService> log) : INotificationService
{
    public async Task SendAsync(string toPhone, string message, bool preferWhatsApp = false, CancellationToken ct = default)
    {
        if (preferWhatsApp)
        {
            var ok = await whatsApp.TrySendAsync(toPhone, message, ct);
            if (ok)
            {
                log.LogDebug("✓ Delivered via WhatsApp to {Phone}", toPhone);
                return;
            }
            log.LogInformation("↪ WhatsApp delivery failed for {Phone}, falling back to SMS", toPhone);
        }

        await sms.SendAsync(toPhone, message, ct);
    }

    public async Task NotifyAdminsAsync(string message, CancellationToken ct = default)
    {
        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Phone != null)
            .Select(u => u.Phone!)
            .ToListAsync(ct);

        foreach (var phone in admins.Distinct())
        {
            // Try WhatsApp first for admins (often nicer UX), SMS fallback
            await SendAsync(phone, message, preferWhatsApp: true, ct);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface ISmsService
{
    Task SendAsync(string toPhone, string message, CancellationToken ct = default);

    /// <summary>
    /// Convenience helper — sends the same message to every active Admin user.
    /// </summary>
    Task NotifyAdminsAsync(string message, CancellationToken ct = default);
}

/// <summary>
/// Console-logging SMS service for development.
/// Swap with Fast2SmsService / TwilioSmsService etc. in production
/// by registering a different implementation in Program.cs.
/// </summary>
public class ConsoleSmsService(AppDbContext db, ILogger<ConsoleSmsService> log) : ISmsService
{
    public Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        log.LogInformation("📱 [SMS to {Phone}] {Message}", toPhone, message);
        return Task.CompletedTask;
    }

    public async Task NotifyAdminsAsync(string message, CancellationToken ct = default)
    {
        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Phone != null)
            .Select(u => new { u.Phone, u.FirstName })
            .ToListAsync(ct);

        if (admins.Count == 0)
        {
            log.LogWarning("📱 [SMS broadcast skipped] No active admin recipients found.");
            return;
        }

        foreach (var admin in admins)
        {
            await SendAsync(admin.Phone!, message, ct);
        }
    }
}

/* ─────────────────────────────────────────────────────────────────────────
 * The active production implementation is Fast2SmsService (free tier
 * available — no DLT registration required for Indian SMS).
 * Configure it in appsettings.Development.json:
 *
 *   "Sms": {
 *     "Provider": "Fast2Sms",
 *     "Fast2Sms": { "AuthKey": "YOUR_KEY_HERE" }
 *   }
 *
 * The provider auto-resolves in Program.cs — when AuthKey is empty,
 * the system falls back to ConsoleSmsService for local dev.
 * ───────────────────────────────────────────────────────────────────────── */

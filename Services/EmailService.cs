using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace RealEstateApi.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Console-logging email service used as a fallback when no SMTP config
/// is present. Writes the recipient, subject, and body to the API logs
/// so flows can be exercised locally without a mail server.
/// </summary>
public class ConsoleEmailService(ILogger<ConsoleEmailService> log) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        log.LogInformation(
            "✉️  [EMAIL to {Email}]\n\tSubject: {Subject}\n\tBody: {Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Real SMTP email sender via MailKit. Picked over ConsoleEmailService
/// in Program.cs whenever Email:Host is set in configuration.
///
/// Required config keys:
///   Email:Host    SMTP server (e.g. mail.joseforland.com)
///   Email:Port    465 (implicit TLS) or 587 (STARTTLS)
///   Email:User    SMTP login (usually the full mailbox address)
///   Email:Pass    SMTP password
///   Email:From    "Display Name &lt;noreply@joseforland.com&gt;"
///   Email:UseSsl  "true" → SslOnConnect, "false" / unset → STARTTLS
/// </summary>
public class SmtpEmailService(IConfiguration cfg, ILogger<SmtpEmailService> log) : IEmailService
{
    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var host = cfg["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured");
        var port = int.TryParse(cfg["Email:Port"], out var p) ? p : 587;
        var user = cfg["Email:User"];
        var pass = cfg["Email:Pass"];
        var from = cfg["Email:From"] ?? user ?? "noreply@joseforland.com";
        // Default: STARTTLS on 587. SslOnConnect when the operator explicitly
        // opts in (port 465 / "UseSsl=true"), which most cPanel hosts prefer.
        var useImplicitSsl = port == 465
            || string.Equals(cfg["Email:UseSsl"], "true", StringComparison.OrdinalIgnoreCase);

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(from));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;
        msg.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        // Render egress occasionally hangs on a stalled handshake — 30 s is
        // generous but bounded.
        client.Timeout = 30_000;
        await client.ConnectAsync(
            host, port,
            useImplicitSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
            ct);
        if (!string.IsNullOrEmpty(user))
            await client.AuthenticateAsync(user, pass, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);

        log.LogInformation("✉️  Sent email to {To} (subject: {Subject}) via {Host}:{Port}",
            toEmail, subject, host, port);
    }
}

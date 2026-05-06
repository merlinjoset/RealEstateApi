namespace RealEstateApi.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Console-logging email service for development.
/// Swap with SendGridEmailService / SmtpEmailService in production.
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

/* Production SMTP example (uncomment, install MailKit, register in Program.cs):
 *
 * public class SmtpEmailService(IConfiguration cfg) : IEmailService {
 *   public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default) {
 *     using var msg = new MimeKit.MimeMessage();
 *     msg.From.Add(MimeKit.MailboxAddress.Parse(cfg["Email:From"]));
 *     msg.To.Add(MimeKit.MailboxAddress.Parse(to));
 *     msg.Subject = subject;
 *     msg.Body = new MimeKit.TextPart("html") { Text = body };
 *     using var client = new MailKit.Net.Smtp.SmtpClient();
 *     await client.ConnectAsync(cfg["Email:Host"], int.Parse(cfg["Email:Port"]!), true, ct);
 *     await client.AuthenticateAsync(cfg["Email:User"], cfg["Email:Pass"], ct);
 *     await client.SendAsync(msg, ct);
 *     await client.DisconnectAsync(true, ct);
 *   }
 * }
 */

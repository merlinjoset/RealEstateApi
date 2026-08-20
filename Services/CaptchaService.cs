using System.Security.Cryptography;
using System.Text;

namespace RealEstateApi.Services;

public interface ICaptchaService
{
    /// <summary>Make a new challenge: a signed token + an SVG image (data URI).</summary>
    (string Token, string ImageDataUri) Generate();

    /// <summary>True when <paramref name="answer"/> matches the code baked into
    /// <paramref name="token"/> and the token hasn't expired.</summary>
    bool Verify(string? token, string? answer);
}

/// <summary>
/// Self-contained image CAPTCHA for the public register + inquiry forms — no
/// third-party service. The correct code is never sent to the browser: it's
/// HMAC-signed into an opaque token (keyed on Jwt:Key), so the server can
/// verify the typed answer statelessly. Tokens expire after a few minutes.
///
/// Defeats the generic bots we saw (which POST straight to the endpoint with
/// no captcha round-trip). For OCR-resistant protection against targeted
/// solvers, Cloudflare Turnstile is the stronger option.
/// </summary>
public class CaptchaService(IConfiguration cfg) : ICaptchaService
{
    // Unambiguous alphabet — no 0/O, 1/I/L to avoid honest failures.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Length = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(4);

    private byte[] Key => Encoding.UTF8.GetBytes(
        cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key required for captcha signing"));

    public (string Token, string ImageDataUri) Generate()
    {
        var code = string.Concat(Enumerable.Range(0, Length)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]));
        var expiry = DateTimeOffset.UtcNow.Add(Ttl).ToUnixTimeSeconds();
        var token = $"{expiry}.{Sign(code, expiry)}";
        var svg = BuildSvg(code);
        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return (token, dataUri);
    }

    public bool Verify(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer)) return false;
        var parts = token.Split('.', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var expiry)) return false;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return false; // expired
        var expected = Sign(answer.Trim().ToUpperInvariant(), expiry);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[1]));
    }

    private string Sign(string codeUpper, long expiry)
    {
        using var h = new HMACSHA256(Key);
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{codeUpper}|{expiry}")));
    }

    private static string BuildSvg(string code)
    {
        const int w = 170, h = 56;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.Append($"<rect width='{w}' height='{h}' rx='8' fill='#f3f4f6'/>");
        // noise lines
        for (var i = 0; i < 6; i++)
            sb.Append($"<line x1='{R(w)}' y1='{R(h)}' x2='{R(w)}' y2='{R(h)}' stroke='#{Col(170, 205)}' stroke-width='1'/>");
        // characters — each jittered, rotated, coloured
        for (var i = 0; i < code.Length; i++)
        {
            int x = 18 + i * 30 + RandomNumberGenerator.GetInt32(-3, 4);
            int y = 38 + RandomNumberGenerator.GetInt32(-5, 6);
            int rot = RandomNumberGenerator.GetInt32(-25, 26);
            int fs = RandomNumberGenerator.GetInt32(27, 35);
            sb.Append($"<text x='{x}' y='{y}' font-family='monospace' font-weight='bold' font-size='{fs}' " +
                      $"fill='#{Col(30, 110)}' transform='rotate({rot} {x} {y})'>{code[i]}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static int R(int max) => RandomNumberGenerator.GetInt32(max);
    private static string Col(int min, int max) =>
        $"{R2(min, max):X2}{R2(min, max):X2}{R2(min, max):X2}";
    private static int R2(int min, int max) => RandomNumberGenerator.GetInt32(min, max);
}

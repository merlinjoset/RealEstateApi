using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RealEstateApi.Data;
using RealEstateApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key configuration is missing or too short. " +
        "Set it via env var Jwt__Key on Render (or in appsettings) " +
        "to a random string of at least 32 characters. " +
        $"Current length: {jwtKey?.Length ?? 0}.");
}

// Disable the historical inbound-claim mapping that rewrites short JWT
// claim names like "role" to the schema URI form — without this, the role
// in the token sometimes lands on a different claim type than the
// [Authorize(Roles="Admin")] check expects, and admins get a silent 403.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            // Be explicit about which claim types Authorize() reads — the
            // TokenService writes both of these on every access token.
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IRsaKeyService, RsaKeyService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITestimonialService, TestimonialService>();
builder.Services.AddScoped<ISmsTemplateService, SmsTemplateService>();
builder.Services.AddScoped<ISiteSettingService, SiteSettingService>();
// SMS — Fast2SMS when configured (free tier, no DLT), Console fallback for dev.
var fast2SmsKey = builder.Configuration["Sms:Fast2Sms:AuthKey"];
if (!string.IsNullOrWhiteSpace(fast2SmsKey))
{
    builder.Services.AddHttpClient<Fast2SmsService>();
    builder.Services.AddScoped<ISmsService, Fast2SmsService>();
}
else
{
    builder.Services.AddScoped<ISmsService, ConsoleSmsService>();
}
builder.Services.AddScoped<IEmailService, ConsoleEmailService>();

// WhatsApp + smart-routing notification (tries WhatsApp first, SMS fallback).
// Meta Cloud API takes over when WhatsApp:Meta:AccessToken is configured; the
// ConsoleWhatsAppService stub stays as the local-dev fallback so behaviour is
// predictable when running without credentials.
var metaWaToken = builder.Configuration["WhatsApp:Meta:AccessToken"];
if (!string.IsNullOrWhiteSpace(metaWaToken))
{
    builder.Services.AddHttpClient<MetaCloudWhatsAppService>();
    builder.Services.AddScoped<IWhatsAppService, MetaCloudWhatsAppService>();
}
else
{
    builder.Services.AddScoped<IWhatsAppService, ConsoleWhatsAppService>();
}
builder.Services.AddScoped<INotificationService, NotificationService>();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000", "http://localhost:3001"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ── Controllers / Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Real Estate API",
        Version = "v1",
        Description = "REST API for the real estate platform",
    });

    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

var app = builder.Build();

// ── Provider banner — makes Render logs immediately tell you which adapters
//    are active for SMS / WhatsApp without having to trigger a test send.
var bootLog = app.Services.GetRequiredService<ILogger<Program>>();
var smsProvider = string.IsNullOrWhiteSpace(fast2SmsKey)
    ? "Console (dev stub)"
    : $"Fast2SMS (route={builder.Configuration["Sms:Fast2Sms:Route"] ?? "otp"})";
var waProvider  = string.IsNullOrWhiteSpace(metaWaToken) ? "Console (dev stub)" : "Meta Cloud API";
bootLog.LogInformation("📨 Messaging providers — SMS: {Sms} · WhatsApp: {WA}", smsProvider, waProvider);

// ── Migrate & seed on startup ─────────────────────────────────────────────────
// Wrapped in try/catch so a transient DB issue doesn't crash the container
// before we can even serve /healthz or Swagger to diagnose it.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "(unset)";
    var safeConnStr = System.Text.RegularExpressions.Regex.Replace(connStr,
        @"Password=[^;]+", "Password=***");

    try
    {
        logger.LogInformation("🗄️  Applying EF migrations against: {ConnStr}", safeConnStr);
        await db.Database.MigrateAsync();
        logger.LogInformation("✓ Migrations applied successfully.");

        // ── Production admin bootstrap ───────────────────────────────────
        // The seeded admin in DbContext uses a known dev password ("Admin@123")
        // which is in the source repo. On production, the operator should set
        // `Seed__AdminPassword` to a strong secret — we'll rotate the seeded
        // admin's password to that value the first time the app boots.
        var seedAdminEmail    = builder.Configuration["Seed:AdminEmail"]    ?? "admin@joseforland.com";
        var seedAdminPassword = builder.Configuration["Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(seedAdminPassword))
        {
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == seedAdminEmail);
            if (admin is not null)
            {
                var newHash = BCrypt.Net.BCrypt.HashPassword(seedAdminPassword);
                if (admin.PasswordHash != newHash) // only re-hash when actually different
                {
                    admin.PasswordHash = newHash;
                    admin.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    logger.LogWarning(
                        "🔐 Admin password rotated from Seed:AdminPassword env var for {Email}. " +
                        "Remove this env var once you've signed in successfully.", seedAdminEmail);
                }
            }
            else
            {
                logger.LogWarning("Seed:AdminPassword set but no admin user exists with email {Email} — skipping rotation.", seedAdminEmail);
            }
        }
        else if (builder.Environment.IsProduction())
        {
            logger.LogWarning(
                "⚠️  Production deploy detected with default admin password ('Admin@123' from source). " +
                "Set Seed__AdminPassword env var to a strong secret and redeploy to rotate immediately.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "❌ Migration failed. App will continue serving requests, but DB-dependent " +
            "endpoints will error. Check the connection string env var: {ConnStr}",
            safeConnStr);
        // Don't rethrow — keep the app alive so /healthz works and the operator
        // can diagnose without a crash loop.
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Swagger — available everywhere so deployments can be sanity-checked from the browser
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Real Estate API v1"));

// Skip HTTPS redirect when running behind a TLS-terminating proxy (Render, Fly.io, etc.)
// Detected via DOTNET_RUNNING_IN_CONTAINER which Docker images set, or by Development env.
var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
if (!inContainer && !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

// ── Media (re-hosted property images) ────────────────────────────────────────
// Files swept from the legacy WordPress install land here. Path is
// configurable via Storage:UploadsPath — set to a Render-disk mount point
// (e.g. /var/data/uploads) in production so files survive deploys. Defaults
// to ./uploads under the API working dir for local development.
{
    var rawPath = app.Configuration["Storage:UploadsPath"] ?? "uploads";
    var uploadsPath = Path.IsPathRooted(rawPath)
        ? rawPath
        : Path.Combine(app.Environment.ContentRootPath, rawPath);
    Directory.CreateDirectory(uploadsPath);

    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath  = "/media",
        OnPrepareResponse = ctx =>
        {
            // Long-lived cache — every file is content-addressed by its
            // path under wp-content/uploads/YYYY/MM/, which never changes.
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        },
    });
    bootLog.LogInformation("🖼 Media uploads served from {Path} → /media/*", uploadsPath);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Simple health-check endpoint Render can poll
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();

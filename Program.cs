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
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
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
        };
    });

builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITestimonialService, TestimonialService>();
builder.Services.AddScoped<ISmsTemplateService, SmsTemplateService>();
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

// WhatsApp + smart-routing notification (tries WhatsApp first, SMS fallback)
builder.Services.AddScoped<IWhatsAppService, ConsoleWhatsAppService>();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Simple health-check endpoint Render can poll
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();

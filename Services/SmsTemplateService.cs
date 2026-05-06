using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface ISmsTemplateService
{
    /// <summary>Render a template by key with the supplied variables.</summary>
    Task<string?> RenderAsync(string key, IDictionary<string, string?> vars, CancellationToken ct = default);

    Task<IReadOnlyList<SmsTemplateDto>> GetAllAsync(CancellationToken ct = default);
    Task<SmsTemplateDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SmsTemplateDto> CreateAsync(CreateSmsTemplateRequest req, CancellationToken ct = default);
    Task<SmsTemplateDto?> UpdateAsync(int id, UpdateSmsTemplateRequest req, CancellationToken ct = default);
    Task<SmsTemplateDto?> ToggleActiveAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class SmsTemplateService(AppDbContext db, ILogger<SmsTemplateService> log) : ISmsTemplateService
{
    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private static SmsTemplateDto ToDto(SmsTemplate t) => new(
        t.Id, t.Key, t.Label, t.Description, t.Body, t.AvailableVars,
        t.IsActive, t.CreatedAt, t.UpdatedAt);

    public async Task<string?> RenderAsync(string key, IDictionary<string, string?> vars, CancellationToken ct = default)
    {
        var tpl = await db.SmsTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == key && t.IsActive, ct);

        if (tpl is null)
        {
            log.LogWarning("📝 SMS template '{Key}' not found or inactive — message will be skipped.", key);
            return null;
        }

        // {var} → vars[var] (empty string for missing keys, no exception thrown)
        return PlaceholderRegex.Replace(tpl.Body, match =>
        {
            var name = match.Groups[1].Value;
            return vars.TryGetValue(name, out var value) ? value ?? "" : "";
        });
    }

    public async Task<IReadOnlyList<SmsTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await db.SmsTemplates
            .OrderBy(t => t.Key)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<SmsTemplateDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SmsTemplates.FindAsync([id], ct);
        return t is null ? null : ToDto(t);
    }

    public async Task<SmsTemplateDto> CreateAsync(CreateSmsTemplateRequest req, CancellationToken ct = default)
    {
        var t = new SmsTemplate
        {
            Key = req.Key.Trim(),
            Label = req.Label.Trim(),
            Description = req.Description?.Trim(),
            Body = req.Body,
            AvailableVars = req.AvailableVars,
            IsActive = req.IsActive,
        };
        db.SmsTemplates.Add(t);
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<SmsTemplateDto?> UpdateAsync(int id, UpdateSmsTemplateRequest req, CancellationToken ct = default)
    {
        var t = await db.SmsTemplates.FindAsync([id], ct);
        if (t is null) return null;

        t.Label = req.Label.Trim();
        t.Description = req.Description?.Trim();
        t.Body = req.Body;
        t.AvailableVars = req.AvailableVars;
        t.IsActive = req.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<SmsTemplateDto?> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SmsTemplates.FindAsync([id], ct);
        if (t is null) return null;
        t.IsActive = !t.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SmsTemplates.FindAsync([id], ct);
        if (t is null) return false;
        db.SmsTemplates.Remove(t);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

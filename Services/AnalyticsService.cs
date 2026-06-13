using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface IAnalyticsService
{
    Task RecordAsync(TrackPageViewRequest req);
    Task<AnalyticsSummaryDto> GetSummaryAsync(int days);
}

public class AnalyticsService(AppDbContext db) : IAnalyticsService
{
    private static string Cap(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? "" : (s.Length > max ? s[..max] : s).Trim();

    public async Task RecordAsync(TrackPageViewRequest req)
    {
        // Strip any query string / fragment so paths group cleanly, and cap
        // lengths so a crafted request can't bloat a row.
        var path = Cap(req.Path, 300);
        if (string.IsNullOrEmpty(path)) path = "/";
        var qi = path.IndexOfAny(['?', '#']);
        if (qi >= 0) path = qi == 0 ? "/" : path[..qi];

        db.PageViews.Add(new PageView
        {
            Path = path,
            Referrer = Cap(req.Referrer, 150),
            VisitorId = Cap(req.VisitorId, 64),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<AnalyticsSummaryDto> GetSummaryAsync(int days)
    {
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-(days - 1));            // inclusive start of window
        var prevSince = since.AddDays(-days);             // previous comparable window

        var window = db.PageViews.Where(p => p.CreatedAt >= since);

        var totalViews = await window.CountAsync();
        var uniqueVisitors = await window
            .Where(p => p.VisitorId != null && p.VisitorId != "")
            .Select(p => p.VisitorId).Distinct().CountAsync();
        var viewsToday = await db.PageViews.CountAsync(p => p.CreatedAt >= today);
        var viewsPrev = await db.PageViews
            .CountAsync(p => p.CreatedAt >= prevSince && p.CreatedAt < since);

        // Daily counts (group by calendar date), then fill gaps in memory so
        // the chart has one bar per day even on zero-traffic days.
        var rawDaily = await window
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();
        var dailyMap = rawDaily.ToDictionary(r => r.Date, r => r.Count);
        var daily = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new DailyCountDto(d.ToString("yyyy-MM-dd"), dailyMap.GetValueOrDefault(d, 0)))
            .ToList();

        var topPages = (await window
            .GroupBy(p => p.Path)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .Take(10)
            .ToListAsync())
            .Select(r => new LabelCountDto(r.Label, r.Count))
            .ToList();

        var topReferrers = (await window
            .GroupBy(p => p.Referrer)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .Take(10)
            .ToListAsync())
            .Select(r => new LabelCountDto(
                string.IsNullOrWhiteSpace(r.Label) ? "Direct / none" : r.Label!, r.Count))
            .ToList();

        return new AnalyticsSummaryDto(
            days, totalViews, uniqueVisitors, viewsToday, viewsPrev,
            daily, topPages, topReferrers);
    }
}

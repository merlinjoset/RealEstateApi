namespace RealEstateApi.DTOs;

/// <summary>Payload the SPA POSTs on each route change. All fields optional-ish;
/// the server sanitises and caps lengths.</summary>
public record TrackPageViewRequest(string? Path, string? Referrer, string? VisitorId);

public record DailyCountDto(string Date, int Count);
public record LabelCountDto(string Label, int Count);

/// <summary>Aggregated traffic over the last <see cref="Days"/> days, for the
/// admin Traffic dashboard.</summary>
public record AnalyticsSummaryDto(
    int Days,
    int TotalViews,
    int UniqueVisitors,
    int ViewsToday,
    int ViewsPreviousPeriod,
    List<DailyCountDto> Daily,
    List<LabelCountDto> TopPages,
    List<LabelCountDto> TopReferrers
);

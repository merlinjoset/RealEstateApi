using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record SiteSettingDto(
    string FacebookUrl,
    string InstagramUrl,
    string YoutubeUrl,
    string WebsiteUrl,
    DateTime UpdatedAt
);

public record UpdateSiteSettingRequest(
    [Url] string? FacebookUrl,
    [Url] string? InstagramUrl,
    [Url] string? YoutubeUrl,
    [Url] string? WebsiteUrl
);

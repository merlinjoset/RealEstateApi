using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTOs;

public record SmsTemplateDto(
    int Id,
    string Key,
    string Label,
    string? Description,
    string Body,
    string? AvailableVars,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateSmsTemplateRequest(
    [Required, MaxLength(100)] string Key,
    [Required, MaxLength(200)] string Label,
    string? Description,
    [Required, MaxLength(2000)] string Body,
    string? AvailableVars,
    bool IsActive = true
);

public record UpdateSmsTemplateRequest(
    [Required, MaxLength(200)] string Label,
    string? Description,
    [Required, MaxLength(2000)] string Body,
    string? AvailableVars,
    bool IsActive = true
);

public record TestSmsRequest(
    [Required] string Phone,
    Dictionary<string, string?>? Vars = null
);

public record TestSmsResult(
    bool Sent,
    string Provider,
    string RenderedBody,
    string Phone,
    string? Note
);

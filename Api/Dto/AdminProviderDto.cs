namespace Api.Dto;

public sealed record AdminProviderDto(
    int Id,
    string Slug,
    string Name,
    string? Abbreviation);

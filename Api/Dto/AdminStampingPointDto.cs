namespace Api.Dto;

public sealed record AdminStampingPointRequestDto(
    string? Provider,
    string? Series,
    int? Number,
    string Name,
    decimal Latitude,
    decimal Longitude,
    string? ExternalId,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public sealed record AdminStampingPointResponseDto(
    int Id,
    string Provider,
    string Series,
    int? Number,
    string Name,
    decimal Latitude,
    decimal Longitude,
    string ExternalId,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public sealed record AdminSavePointsResponseDto(
    int Count,
    IReadOnlyList<AdminStampingPointResponseDto> Points);

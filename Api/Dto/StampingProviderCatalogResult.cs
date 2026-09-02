namespace Api.Dto;

public sealed record StampingProviderCatalogResult(
    int OverallCount,
    int TotalPoints,
    int VisitedPoints,
    IReadOnlyList<StampingProviderDetailsDto> Providers);

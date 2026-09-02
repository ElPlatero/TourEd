using Api.Dto;

namespace Api.Controllers.Providers;

public sealed record GetStampingProvidersResponse(
    int OverallCount,
    int TotalPoints,
    int VisitedPoints,
    IEnumerable<StampingProviderDetailsDto> StampingProviders);

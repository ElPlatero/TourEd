using Api.Dto;

namespace Api.Controllers.Providers;

public sealed record GetStampingProvidersResponse(
    int OverallCount,
    IEnumerable<StampingProviderDetailsDto> StampingProviders);

using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record StampingProviderDto(string Slug, string Name, string? Abbreviation)
{
    public static StampingProviderDto Create(StampingProvider provider) => new(provider.Slug, provider.Name, provider.Abbreviation);
}

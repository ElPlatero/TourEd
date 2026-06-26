using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record StampingProviderDto(string Slug, string Name)
{
    public static StampingProviderDto Create(StampingProvider provider) => new(provider.Slug, provider.Name);
}

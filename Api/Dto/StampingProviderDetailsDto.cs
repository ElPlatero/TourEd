using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public sealed record StampingProviderDetailsDto(
    string Slug,
    string Name,
    string? Abbreviation,
    bool IsAnonymousAccessAllowed,
    string? Description,
    string? WebsiteUrl)
{
    public static StampingProviderDetailsDto Create(StampingProvider provider)
        => new(provider.Slug, provider.Name, provider.Abbreviation, provider.IsAnonymousAccessAllowed, provider.Description, GetPublicWebsiteUrl(provider.WebsiteUri));

    private static string? GetPublicWebsiteUrl(Uri? websiteUri)
    {
        if (websiteUri is not { IsAbsoluteUri: true })
        {
            return null;
        }

        return string.Equals(websiteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(websiteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? websiteUri.AbsoluteUri
            : null;
    }
}

using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public sealed record StampingProviderDetailsDto(
    string Slug,
    string Name,
    string? Abbreviation,
    bool IsAnonymousAccessAllowed,
    string? Description,
    string? WebsiteUrl,
    string? DataSourceAttribution,
    string? DataSourceUrl,
    string? DataLicenseName,
    string? DataLicenseUrl,
    bool HasPublicDataDownload)
{
    public static StampingProviderDetailsDto Create(StampingProvider provider)
        => new(
            provider.Slug,
            provider.Name,
            provider.Abbreviation,
            provider.IsAnonymousAccessAllowed,
            provider.Description,
            GetPublicHttpUrl(provider.WebsiteUri),
            provider.DataSourceAttribution,
            GetPublicHttpUrl(provider.DataSourceUri),
            provider.DataLicenseName,
            GetPublicHttpUrl(provider.DataLicenseUri),
            provider.IsAnonymousAccessAllowed && provider.DataImportedAt is not null);

    private static string? GetPublicHttpUrl(Uri? websiteUri)
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

using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public sealed record StampingProviderDetailsDto(
    string Slug,
    string Name,
    string? Abbreviation,
    bool IsEnabled,
    bool IsDataReady,
    bool IsAnonymousAccessAllowed,
    int? TotalPoints,
    int? VisitedPoints,
    string? Description,
    string? WebsiteUrl,
    string? DataSourceAttribution,
    string? DataSourceUrl,
    string? DataLicenseName,
    string? DataLicenseUrl,
    bool HasPublicDataDownload)
{
    public static StampingProviderDetailsDto Create(
        StampingProvider provider,
        bool isEnabled,
        bool isDataReady,
        int? totalPoints,
        int? visitedPoints)
        => new(
            provider.Slug,
            provider.Name,
            provider.Abbreviation,
            isEnabled,
            isDataReady,
            isDataReady,
            isDataReady ? totalPoints : null,
            isDataReady ? visitedPoints : null,
            provider.Description,
            GetPublicHttpUrl(provider.WebsiteUri),
            provider.DataSourceAttribution,
            GetPublicHttpUrl(provider.DataSourceUri),
            provider.DataLicenseName,
            GetPublicHttpUrl(provider.DataLicenseUri),
            isEnabled && isDataReady && provider.DataImportedAt is not null);

    public static StampingProviderDetailsDto Create(StampingProvider provider)
        => Create(provider, true, provider.IsAnonymousAccessAllowed, null, null);

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

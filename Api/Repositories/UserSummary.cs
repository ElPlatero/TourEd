namespace Api.Repositories;

/// <summary>Administrative overview of a user, projected in the database.</summary>
public sealed record UserSummary(
    int Id,
    string Email,
    bool IsGoogleLinked,
    string? DefaultProviderSlug,
    IReadOnlyList<string> ProviderSlugs,
    int VisitCount);

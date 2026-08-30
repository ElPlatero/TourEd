namespace Api.Managers;

public record StampingProviderFilter(int? ProviderId, bool IsAnonymousOnly = false)
{
    public bool IncludesAllProviders => ProviderId == null;

    public static StampingProviderFilter All { get; } = new((int?)null);
    public static StampingProviderFilter Anonymous { get; } = new((int?)null, true);
    public static StampingProviderFilter Single(int providerId) => new(providerId);
}

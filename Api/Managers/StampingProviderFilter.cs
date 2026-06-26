namespace Api.Managers;

public record StampingProviderFilter(int? ProviderId)
{
    public bool IncludesAllProviders => ProviderId == null;

    public static StampingProviderFilter All { get; } = new((int?)null);
    public static StampingProviderFilter Single(int providerId) => new(providerId);
}

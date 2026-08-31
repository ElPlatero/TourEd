namespace Api.Managers;

public record StampingProviderFilter(int? ProviderId, int? UserId = null, bool IsAnonymousOnly = false)
{
    public bool IncludesAllProviders => ProviderId == null;

    public static StampingProviderFilter All { get; } = new((int?)null);
    public static StampingProviderFilter Anonymous { get; } = new((int?)null, null, true);
    public static StampingProviderFilter Single(int providerId) => new(providerId);
    public static StampingProviderFilter ForUser(int userId) => new(null, userId);
    public static StampingProviderFilter SingleForUser(int providerId, int userId) => new(providerId, userId);
}

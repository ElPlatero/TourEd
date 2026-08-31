namespace Api.Dto;

public sealed record AdminUserDto(
    int Id,
    string Email,
    bool IsGoogleLinked,
    string? DefaultProvider,
    IReadOnlyList<string> Providers);

public sealed record UpdateAdminUserProvidersRequestDto(
    IReadOnlyList<string> Providers,
    string? DefaultProvider);

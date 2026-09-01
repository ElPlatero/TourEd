namespace Api.Dto;

public sealed record AdminAuditEntryDto(
    int Id,
    DateTime CreatedAt,
    int ActorUserId,
    string Action,
    int? TargetUserId,
    int? RegistrationRequestId,
    string? ProviderSlug);

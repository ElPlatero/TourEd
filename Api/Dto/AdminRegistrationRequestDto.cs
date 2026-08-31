namespace Api.Dto;

public sealed record AdminRegistrationRequestDto(
    int Id,
    string GoogleSubject,
    string Email,
    string Status,
    DateTime CreatedAt,
    DateTime? DecidedAt);

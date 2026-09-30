using Api.ErrorHandling;

namespace Api.Repositories;

public sealed class RegistrationRequestAlreadyDecidedException(int requestId) : ConflictException(
    $"Registration request {requestId} has already been decided.");

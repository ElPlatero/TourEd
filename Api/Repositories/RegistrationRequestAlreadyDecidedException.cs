namespace Api.Repositories;

public sealed class RegistrationRequestAlreadyDecidedException(int requestId) : Exception(
    $"Registration request {requestId} has already been decided.");

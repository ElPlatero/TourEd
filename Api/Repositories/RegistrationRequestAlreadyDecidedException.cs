using TourEd.Lib.Abstractions.Exceptions;

namespace Api.Repositories;

public sealed class RegistrationRequestAlreadyDecidedException(int requestId) : ConflictException(
    $"Registration request {requestId} has already been decided.");

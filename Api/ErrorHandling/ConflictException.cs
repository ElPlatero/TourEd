namespace Api.ErrorHandling;

/// <summary>The request conflicts with the current state of the resource (HTTP 409).</summary>
public class ConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

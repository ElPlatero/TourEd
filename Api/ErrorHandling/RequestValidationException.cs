namespace Api.ErrorHandling;

/// <summary>The request is invalid; the message is safe to return to the client (HTTP 400).</summary>
public class RequestValidationException(string message) : Exception(message);

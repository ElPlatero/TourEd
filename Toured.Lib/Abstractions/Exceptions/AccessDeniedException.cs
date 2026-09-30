namespace TourEd.Lib.Abstractions.Exceptions;

/// <summary>The authenticated caller may not access the requested resource (HTTP 403).</summary>
public class AccessDeniedException(string message) : Exception(message);

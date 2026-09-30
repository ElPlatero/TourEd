namespace Api.Authentication;

public sealed record GoogleLoginClaims(string Subject, string Email, bool EmailVerified);

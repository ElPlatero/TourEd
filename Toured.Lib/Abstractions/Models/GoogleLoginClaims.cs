namespace TourEd.Lib.Abstractions.Models;

public sealed record GoogleLoginClaims(string Subject, string Email, bool EmailVerified);

namespace TourEd.Lib.Abstractions.Exceptions;

public enum GoogleLoginRejectionReason
{
    InvalidClaims,
    EmailNotVerified,
    UnknownUser,
    SubjectAlreadyBound,
    UserAlreadyBound,
    RegistrationPending
}

public sealed class GoogleLoginRejectedException : InvalidOperationException
{
    public GoogleLoginRejectedException(GoogleLoginRejectionReason reason)
        : base(GetMessage(reason))
    {
        Reason = reason;
    }

    public GoogleLoginRejectionReason Reason { get; }

    private static string GetMessage(GoogleLoginRejectionReason reason)
        => reason switch
        {
            GoogleLoginRejectionReason.InvalidClaims => "The Google identity claims are incomplete.",
            GoogleLoginRejectionReason.EmailNotVerified => "The Google email address is not verified.",
            GoogleLoginRejectionReason.UnknownUser => "The Google identity is not assigned to a TourEd user.",
            GoogleLoginRejectionReason.SubjectAlreadyBound => "The Google identity is assigned to another TourEd user.",
            GoogleLoginRejectionReason.UserAlreadyBound => "The TourEd user is assigned to another Google identity.",
            GoogleLoginRejectionReason.RegistrationPending => "The registration request is pending administrator approval.",
            _ => "The Google identity cannot be used to sign in."
        };
}

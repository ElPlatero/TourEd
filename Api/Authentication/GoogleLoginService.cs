using System.Security.Claims;
using Api.Entities;
using Api.Repositories;

namespace Api.Authentication;

public sealed class GoogleLoginService
{
    private const string AuthenticationType = "TourEdGoogleLogin";
    private readonly UserRepository _users;
    private readonly RegistrationRequestRepository _registrationRequests;

    public GoogleLoginService(UserRepository users, RegistrationRequestRepository registrationRequests)
    {
        _users = users;
        _registrationRequests = registrationRequests;
    }

    public async Task<User> AuthenticateAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default)
    {
        var result = await ProcessLoginAsync(claims, cancellationToken);
        if (result.Status == GoogleLoginStatus.Authenticated && result.User is not null)
        {
            return result.User;
        }

        if (result.Status == GoogleLoginStatus.Pending)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.RegistrationPending);
        }

        if (result.Status == GoogleLoginStatus.Rejected)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.RegistrationRejected);
        }

        throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UnknownUser);
    }

    public async Task<GoogleLoginResult> ProcessLoginAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(claims.Subject) || string.IsNullOrWhiteSpace(claims.Email))
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.InvalidClaims);
        }

        if (!claims.EmailVerified)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.EmailNotVerified);
        }

        var userBySubject = await _users.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken);
        if (userBySubject != null)
        {
            await _registrationRequests.MarkRegistrationRequestApprovedAsync(claims.Subject, cancellationToken);
            return CreateAuthenticatedResult(userBySubject);
        }

        var userByEmail = await _users.GetUserOrDefaultAsync(claims.Email, cancellationToken);
        if (userByEmail != null)
        {
            if (userByEmail.GoogleSubject != null)
            {
                throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UserAlreadyBound);
            }

            if (await _users.TryBindGoogleSubjectAsync(userByEmail.Id, claims.Subject, cancellationToken))
            {
                var boundUser = await _users.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken)
                       ?? throw new InvalidOperationException("The Google account binding could not be loaded.");
                await _registrationRequests.MarkRegistrationRequestApprovedAsync(claims.Subject, cancellationToken);
                return CreateAuthenticatedResult(boundUser);
            }

            userBySubject = await _users.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken);
            if (userBySubject?.Id == userByEmail.Id)
            {
                await _registrationRequests.MarkRegistrationRequestApprovedAsync(claims.Subject, cancellationToken);
                return CreateAuthenticatedResult(userBySubject);
            }

            if (userBySubject != null)
            {
                throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.SubjectAlreadyBound);
            }

            userByEmail = await _users.GetUserOrDefaultAsync(claims.Email, cancellationToken);
            if (userByEmail?.GoogleSubject == claims.Subject)
            {
                await _registrationRequests.MarkRegistrationRequestApprovedAsync(claims.Subject, cancellationToken);
                return CreateAuthenticatedResult(userByEmail);
            }

            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UserAlreadyBound);
        }

        var registrationRequest = await _registrationRequests.RecordOrUpdateRegistrationRequestAsync(
            claims.Subject,
            claims.Email,
            cancellationToken);

        return new GoogleLoginResult(
            registrationRequest.Status == RegistrationRequestStatus.Rejected
                ? GoogleLoginStatus.Rejected
                : GoogleLoginStatus.Pending,
            null,
            registrationRequest,
            null);
    }

    public async Task<ClaimsPrincipal> CreatePrincipalAsync(
        GoogleLoginClaims claims,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessLoginAsync(claims, cancellationToken);
        if (result.Status != GoogleLoginStatus.Authenticated || result.Principal is null)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.RegistrationPending);
        }

        return result.Principal;
    }

    private static GoogleLoginResult CreateAuthenticatedResult(User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(Constants.ClaimsNames.UserId, user.Id.ToString()),
            new Claim(Constants.ClaimsNames.UserEmail, user.Email)
        ], AuthenticationType);

        return new GoogleLoginResult(
            GoogleLoginStatus.Authenticated,
            user,
            null,
            new ClaimsPrincipal(identity));
    }
}

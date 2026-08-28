using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Services;

public sealed class GoogleLoginService : IGoogleLoginService
{
    private readonly IUserService _userService;

    public GoogleLoginService(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<User> AuthenticateAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(claims.Subject) || string.IsNullOrWhiteSpace(claims.Email))
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.InvalidClaims);
        }

        if (!claims.EmailVerified)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.EmailNotVerified);
        }

        var userBySubject = await _userService.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken);
        if (userBySubject != null)
        {
            return userBySubject;
        }

        var userByEmail = await _userService.GetUserOrDefaultAsync(claims.Email, cancellationToken);
        if (userByEmail == null)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UnknownUser);
        }

        if (userByEmail.GoogleSubject != null)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UserAlreadyBound);
        }

        if (await _userService.TryBindGoogleSubjectAsync(userByEmail.Id, claims.Subject, cancellationToken))
        {
            return await _userService.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken)
                   ?? throw new InvalidOperationException("The Google account binding could not be loaded.");
        }

        userBySubject = await _userService.GetUserByGoogleSubjectOrDefaultAsync(claims.Subject, cancellationToken);
        if (userBySubject?.Id == userByEmail.Id)
        {
            return userBySubject;
        }

        if (userBySubject != null)
        {
            throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.SubjectAlreadyBound);
        }

        userByEmail = await _userService.GetUserOrDefaultAsync(claims.Email, cancellationToken);
        if (userByEmail?.GoogleSubject == claims.Subject)
        {
            return userByEmail;
        }

        throw new GoogleLoginRejectedException(GoogleLoginRejectionReason.UserAlreadyBound);
    }
}

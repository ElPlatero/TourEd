using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public sealed class RegistrationManager
{
    private readonly RegistrationRequestRepository _registrationRequests;
    private readonly UserRepository _users;
    private readonly AdminAuditRepository _audit;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public RegistrationManager(
        RegistrationRequestRepository registrationRequests,
        UserRepository users,
        AdminAuditRepository audit,
        IUnitOfWorkFactory unitOfWorkFactory)
    {
        _registrationRequests = registrationRequests;
        _users = users;
        _audit = audit;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<List<AdminRegistrationRequestDto>> GetRegistrationRequestsAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        RegistrationRequestStatus? requestedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RegistrationRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return [];
            }
            requestedStatus = parsedStatus;
        }

        return (await _registrationRequests.GetRequestsAsync(requestedStatus, cancellationToken))
            .Select(CreateDto)
            .ToList();
    }

    public async Task<AdminRegistrationRequestDto?> ApproveRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var request = await _registrationRequests.GetRequestForUpdateAsync(id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            throw new RegistrationRequestAlreadyDecidedException(id);
        }

        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        await _registrationRequests.ClaimRegistrationDecisionAsync(request, RegistrationRequestStatus.Approved, cancellationToken);

        var existingUser = await _users.FindUserByGoogleSubjectForUpdateAsync(request.GoogleSubject, cancellationToken);
        if (existingUser is null)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            existingUser = await _users.FindUserByNormalizedEmailForUpdateAsync(normalizedEmail, cancellationToken);

            if (existingUser is not null)
            {
                if (existingUser.GoogleSubject is null)
                {
                    existingUser.GoogleSubject = request.GoogleSubject;
                }
                else if (existingUser.GoogleSubject != request.GoogleSubject)
                {
                    throw new InvalidOperationException("User email is already bound to another Google subject.");
                }
            }
            else
            {
                existingUser = new User
                {
                    Email = normalizedEmail,
                    GoogleSubject = request.GoogleSubject,
                    DefaultStampingProviderId = null
                };
                _users.Add(existingUser);
            }

            await _users.SaveChangesAsync(cancellationToken);
        }

        _audit.Add(actorUserId, "registration.approved", existingUser.Id, null, request.Id);
        await _users.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return CreateDto(request);
    }

    public async Task<AdminRegistrationRequestDto?> RejectRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var request = await _registrationRequests.GetRequestForUpdateAsync(id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            throw new RegistrationRequestAlreadyDecidedException(id);
        }

        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        await _registrationRequests.ClaimRegistrationDecisionAsync(request, RegistrationRequestStatus.Rejected, cancellationToken);

        _audit.Add(actorUserId, "registration.rejected", null, null, request.Id);
        await _registrationRequests.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return CreateDto(request);
    }

    private static AdminRegistrationRequestDto CreateDto(RegistrationRequest request)
        => new(
            request.Id,
            request.GoogleSubject,
            request.Email,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAt,
            request.DecidedAt);
}

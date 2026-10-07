using AccessRequestHub.Data;
using AccessRequestHub.DTOs;
using AccessRequestHub.Exceptions;
using AccessRequestHub.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AccessRequestHub.Services;

public class AccessRequestService
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _currentUserService;

    public AccessRequestService(AppDbContext db, CurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<(AccessRequest Request, bool IsNew)> CreateRequestAsync(CreateRequestDto payload)
    {
        var currentUser = _currentUserService.User;
        if (currentUser == null)
            throw new AppException(401, "Unauthorized", "User is not authenticated");

        if (currentUser.ManagerId == null)
            throw new AppException(400, "BadRequest", "requester has no manager");

        if (string.IsNullOrWhiteSpace(payload.Justification))
            throw new AppException(400, "BadRequest", "justification cannot be empty");

        var existingRequest = await _db.AccessRequests
            .FirstOrDefaultAsync(r => r.RequesterId == currentUser.Id && r.ClientRequestId == payload.ClientRequestId);

        if (existingRequest != null)
        {
            return (VerifyIdempotencyMatch(existingRequest, payload), false);
        }

        var policy = await _db.PolicyVersions.OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync();
        var policyCode = policy?.Code ?? "v1";

        bool isHighRisk = payload.Environment == EnvironmentType.Production || payload.AccessLevel == AccessLevel.Admin;

        var newRequest = new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = payload.ClientRequestId,
            RequesterId = currentUser.Id,
            ApplicationId = payload.ApplicationId,
            Environment = payload.Environment,
            AccessLevel = payload.AccessLevel,
            Justification = payload.Justification,
            IsHighRisk = isHighRisk,
            Status = RequestStatus.PendingManager,
            PolicyVersionCode = policyCode,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            AccessRequestId = newRequest.Id,
            ActorUserId = currentUser.Id,
            EventType = AuditEventType.Created,
            FromStatus = null,
            ToStatus = RequestStatus.PendingManager,
            Reason = null,
            VersionAfter = 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.AccessRequests.Add(newRequest);
        _db.AuditEvents.Add(auditEvent);

        try
        {
            await _db.SaveChangesAsync();
            return (newRequest, true);
        }
        catch (DbUpdateException)
        {
            // Handle unique constraint violation for (requester_id, client_request_id)
            _db.ChangeTracker.Clear();
            existingRequest = await _db.AccessRequests
                .FirstOrDefaultAsync(r => r.RequesterId == currentUser.Id && r.ClientRequestId == payload.ClientRequestId);

            if (existingRequest != null)
            {
                return (VerifyIdempotencyMatch(existingRequest, payload), false);
            }
            throw;
        }
    }

    private AccessRequest VerifyIdempotencyMatch(AccessRequest existingRequest, CreateRequestDto payload)
    {
        if (existingRequest.ApplicationId != payload.ApplicationId ||
            existingRequest.Environment != payload.Environment ||
            existingRequest.AccessLevel != payload.AccessLevel ||
            existingRequest.Justification != payload.Justification)
        {
            throw new AppException(409, "Conflict", "client_request_id already used with a different payload");
        }
        return existingRequest;
    }
}

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

    private User GetCurrentUser()
    {
        var user = _currentUserService.User;
        if (user == null)
            throw new AppException(401, "Unauthorized", "User is not authenticated");
        return user;
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

    public async Task<System.Collections.Generic.List<AccessRequestListDto>> GetMyRequestsAsync()
    {
        var currentUser = GetCurrentUser();
        return await _db.AccessRequests
            .Include(r => r.Application)
            .Where(r => r.RequesterId == currentUser.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AccessRequestListDto(
                r.Id, r.ClientRequestId, r.Application!.Name, r.Environment.ToString(), 
                r.AccessLevel.ToString(), r.IsHighRisk, r.Status.ToString(), r.CreatedAt, r.UpdatedAt))
            .ToListAsync();
    }

    public async Task<AccessRequestDetailDto> GetRequestDetailAsync(Guid id)
    {
        var currentUser = GetCurrentUser();
        var request = await _db.AccessRequests
            .Include(r => r.Application)
            .Include(r => r.Requester)
            .Include(r => r.AuditEvents)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            throw new AppException(404, "NotFound", "Request not found");

        bool canView = request.RequesterId == currentUser.Id ||
                       request.Requester!.ManagerId == currentUser.Id ||
                       request.Application!.SystemOwnerId == currentUser.Id ||
                       currentUser.Role == UserRole.Auditor;

        if (!canView)
            throw new AppException(404, "NotFound", "Request not found");

        var timeline = request.AuditEvents!
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AuditEventDto(
                a.Id, a.ActorUserId, a.EventType.ToString(), a.FromStatus?.ToString(), 
                a.ToStatus.ToString(), a.Reason, a.VersionAfter, a.CreatedAt))
            .ToArray();

        return new AccessRequestDetailDto(
            request.Id, request.ClientRequestId, request.RequesterId, request.Requester!.Email,
            request.ApplicationId, request.Application!.Name, request.Environment.ToString(),
            request.AccessLevel.ToString(), request.Justification, request.IsHighRisk, 
            request.Status.ToString(), request.PolicyVersionCode, request.Version, 
            request.CreatedAt, request.UpdatedAt, timeline
        );
    }

    public async Task<System.Collections.Generic.List<AccessRequestListDto>> GetInboxAsync()
    {
        var currentUser = GetCurrentUser();
        return await _db.AccessRequests
            .Include(r => r.Application)
            .Include(r => r.Requester)
            .Where(r => 
                (r.Status == RequestStatus.PendingManager && r.Requester!.ManagerId == currentUser.Id) ||
                (r.Status == RequestStatus.PendingSystemOwner && r.Application!.SystemOwnerId == currentUser.Id)
            )
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AccessRequestListDto(
                r.Id, r.ClientRequestId, r.Application!.Name, r.Environment.ToString(), 
                r.AccessLevel.ToString(), r.IsHighRisk, r.Status.ToString(), r.CreatedAt, r.UpdatedAt))
            .ToListAsync();
    }

    public async Task<System.Collections.Generic.List<AccessRequestDetailDto>> GetAllRequestsAsync()
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != UserRole.Auditor)
            throw new AppException(403, "Forbidden", "Only auditors can view all requests");

        var requests = await _db.AccessRequests
            .Include(r => r.Application)
            .Include(r => r.Requester)
            .Include(r => r.AuditEvents)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(request => 
        {
            var timeline = request.AuditEvents!
                .OrderBy(a => a.CreatedAt)
                .Select(a => new AuditEventDto(
                    a.Id, a.ActorUserId, a.EventType.ToString(), a.FromStatus?.ToString(), 
                    a.ToStatus.ToString(), a.Reason, a.VersionAfter, a.CreatedAt))
                .ToArray();

            return new AccessRequestDetailDto(
                request.Id, request.ClientRequestId, request.RequesterId, request.Requester!.Email,
                request.ApplicationId, request.Application!.Name, request.Environment.ToString(),
                request.AccessLevel.ToString(), request.Justification, request.IsHighRisk, 
                request.Status.ToString(), request.PolicyVersionCode, request.Version, 
                request.CreatedAt, request.UpdatedAt, timeline
            );
        }).ToList();
    }

    public async Task<AccessRequestDetailDto> ApproveRequestAsync(Guid requestId, int expectedVersion)
    {
        var currentUser = GetCurrentUser();

        var request = await _db.AccessRequests
            .Include(r => r.Application)
            .Include(r => r.Requester)
            .Include(r => r.AuditEvents)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            throw new AppException(404, "NotFound", "Request not found");

        if (request.Status == RequestStatus.Approved || request.Status == RequestStatus.Rejected)
            throw new AppException(409, "Conflict", "Request is already in a terminal state");

        if (request.Version != expectedVersion)
            throw new AppException(409, "Conflict", "Version mismatch - stale data");

        if (currentUser.Id == request.RequesterId)
            throw new AppException(403, "Forbidden", "You cannot approve your own request");

        if (currentUser.Role == UserRole.Auditor)
            throw new AppException(403, "Forbidden", "Auditors cannot approve requests");

        var oldStatus = request.Status;
        RequestStatus newStatus;
        AuditEventType eventType;

        if (request.Status == RequestStatus.PendingManager)
        {
            if (request.Requester!.ManagerId != currentUser.Id)
                throw new AppException(403, "Forbidden", "You are not the manager of this requester");

            newStatus = request.IsHighRisk ? RequestStatus.PendingSystemOwner : RequestStatus.Approved;
            eventType = AuditEventType.ManagerApproved;
        }
        else if (request.Status == RequestStatus.PendingSystemOwner)
        {
            if (request.Application!.SystemOwnerId != currentUser.Id)
                throw new AppException(403, "Forbidden", "You are not the system owner of this application");

            newStatus = RequestStatus.Approved;
            eventType = AuditEventType.OwnerApproved;
        }
        else
        {
            throw new AppException(400, "BadRequest", "Invalid status for approval");
        }

        request.Status = newStatus;
        request.Version += 1;
        request.UpdatedAt = DateTime.UtcNow;

        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            AccessRequestId = request.Id,
            ActorUserId = currentUser.Id,
            EventType = eventType,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            Reason = null,
            VersionAfter = request.Version,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditEvents.Add(auditEvent);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(409, "Conflict", "The request was modified by another user");
        }

        // Return updated detail
        return await GetRequestDetailAsync(request.Id);
    }

    public async Task<AccessRequestDetailDto> RejectRequestAsync(Guid requestId, int expectedVersion, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppException(400, "BadRequest", "rejection reason is required");

        var currentUser = GetCurrentUser();

        var request = await _db.AccessRequests
            .Include(r => r.Application)
            .Include(r => r.Requester)
            .Include(r => r.AuditEvents)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            throw new AppException(404, "NotFound", "Request not found");

        if (request.Status == RequestStatus.Approved || request.Status == RequestStatus.Rejected)
            throw new AppException(409, "Conflict", "Request is already in a terminal state");

        if (request.Version != expectedVersion)
            throw new AppException(409, "Conflict", "Version mismatch - stale data");

        if (currentUser.Id == request.RequesterId)
            throw new AppException(403, "Forbidden", "You cannot reject your own request");

        if (currentUser.Role == UserRole.Auditor)
            throw new AppException(403, "Forbidden", "Auditors cannot reject requests");

        var oldStatus = request.Status;
        AuditEventType eventType;

        if (request.Status == RequestStatus.PendingManager)
        {
            if (request.Requester!.ManagerId != currentUser.Id)
                throw new AppException(403, "Forbidden", "You are not the manager of this requester");

            eventType = AuditEventType.ManagerRejected;
        }
        else if (request.Status == RequestStatus.PendingSystemOwner)
        {
            if (request.Application!.SystemOwnerId != currentUser.Id)
                throw new AppException(403, "Forbidden", "You are not the system owner of this application");

            eventType = AuditEventType.OwnerRejected;
        }
        else
        {
            throw new AppException(400, "BadRequest", "Invalid status for rejection");
        }

        request.Status = RequestStatus.Rejected;
        request.Version += 1;
        request.UpdatedAt = DateTime.UtcNow;

        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            AccessRequestId = request.Id,
            ActorUserId = currentUser.Id,
            EventType = eventType,
            FromStatus = oldStatus,
            ToStatus = RequestStatus.Rejected,
            Reason = reason,
            VersionAfter = request.Version,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditEvents.Add(auditEvent);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(409, "Conflict", "The request was modified by another user");
        }

        return await GetRequestDetailAsync(request.Id);
    }
}

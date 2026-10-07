using AccessRequestHub.Models;
using System;

namespace AccessRequestHub.DTOs;

public record CreateRequestDto(
    Guid ClientRequestId,
    Guid ApplicationId,
    EnvironmentType Environment,
    AccessLevel AccessLevel,
    string Justification
);

public record AccessRequestResponseDto(
    Guid Id,
    Guid ClientRequestId,
    Guid RequesterId,
    Guid ApplicationId,
    string Environment,
    string AccessLevel,
    string Justification,
    bool IsHighRisk,
    string Status,
    string PolicyVersionCode,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AuditEventDto(
    Guid Id,
    Guid ActorUserId,
    string EventType,
    string? FromStatus,
    string ToStatus,
    string? Reason,
    int VersionAfter,
    DateTime CreatedAt
);

public record AccessRequestListDto(
    Guid Id,
    Guid ClientRequestId,
    string ApplicationName,
    string Environment,
    string AccessLevel,
    bool IsHighRisk,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AccessRequestDetailDto(
    Guid Id,
    Guid ClientRequestId,
    Guid RequesterId,
    string RequesterEmail,
    Guid ApplicationId,
    string ApplicationName,
    string Environment,
    string AccessLevel,
    string Justification,
    bool IsHighRisk,
    string Status,
    string PolicyVersionCode,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AuditEventDto[] AuditTimeline
);

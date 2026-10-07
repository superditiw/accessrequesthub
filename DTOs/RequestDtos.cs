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

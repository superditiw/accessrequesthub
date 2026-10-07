using System;

namespace AccessRequestHub.Models;

public class AccessRequest
{
    public Guid Id { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ApplicationId { get; set; }
    
    public EnvironmentType Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public required string Justification { get; set; }
    public bool IsHighRisk { get; set; }
    public RequestStatus Status { get; set; }
    public required string PolicyVersionCode { get; set; }
    
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User? Requester { get; set; }
    public Application? Application { get; set; }
    public PolicyVersion? PolicyVersion { get; set; }
    
    public System.Collections.Generic.ICollection<AuditEvent>? AuditEvents { get; set; }
}

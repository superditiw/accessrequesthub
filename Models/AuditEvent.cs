using System;

namespace AccessRequestHub.Models;

public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid AccessRequestId { get; set; }
    public Guid ActorUserId { get; set; }
    
    public AuditEventType EventType { get; set; }
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public int VersionAfter { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public AccessRequest? AccessRequest { get; set; }
    public User? ActorUser { get; set; }
}

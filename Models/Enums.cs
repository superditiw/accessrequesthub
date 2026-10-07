namespace AccessRequestHub.Models;

public enum UserRole
{
    Employee,
    Auditor
}

public enum RequestStatus
{
    PendingManager,
    PendingSystemOwner,
    Approved,
    Rejected
}

public enum EnvironmentType
{
    NonProduction,
    Production
}

public enum AccessLevel
{
    Read,
    Write,
    Admin
}

public enum AuditEventType
{
    Created,
    ManagerApproved,
    ManagerRejected,
    OwnerApproved,
    OwnerRejected
}

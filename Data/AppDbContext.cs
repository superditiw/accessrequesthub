using AccessRequestHub.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AccessRequestHub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().Property(e => e.Role).HasConversion<string>();
        modelBuilder.Entity<AccessRequest>().Property(e => e.Environment).HasConversion<string>();
        modelBuilder.Entity<AccessRequest>().Property(e => e.AccessLevel).HasConversion<string>();
        modelBuilder.Entity<AccessRequest>().Property(e => e.Status).HasConversion<string>();
        modelBuilder.Entity<AuditEvent>().Property(e => e.EventType).HasConversion<string>();
        modelBuilder.Entity<AuditEvent>().Property(e => e.FromStatus).HasConversion<string>();
        modelBuilder.Entity<AuditEvent>().Property(e => e.ToStatus).HasConversion<string>();

        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();
            b.HasOne(u => u.Manager).WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_users_manager", "manager_id IS NULL OR manager_id <> id"));
            b.ToTable(t => t.HasCheckConstraint("CK_users_role", "role IN ('Employee', 'Auditor')"));
        });

        modelBuilder.Entity<Application>(b =>
        {
            b.HasIndex(a => a.Name).IsUnique();
            b.HasOne(a => a.SystemOwner).WithMany().HasForeignKey(a => a.SystemOwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PolicyVersion>(b =>
        {
            b.HasKey(p => p.Code);
        });

        modelBuilder.Entity<AccessRequest>(b =>
        {
            b.HasOne(a => a.Requester).WithMany().HasForeignKey(a => a.RequesterId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.Application).WithMany().HasForeignKey(a => a.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.PolicyVersion).WithMany().HasForeignKey(a => a.PolicyVersionCode).OnDelete(DeleteBehavior.Restrict);
            
            b.HasIndex(a => new { a.RequesterId, a.ClientRequestId }).IsUnique();
            
            b.Property(a => a.Version).IsConcurrencyToken();
            
            b.ToTable(t => {
                t.HasCheckConstraint("CK_access_requests_status", "status IN ('PendingManager', 'PendingSystemOwner', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_access_requests_environment", "environment IN ('NonProduction', 'Production')");
                t.HasCheckConstraint("CK_access_requests_access_level", "access_level IN ('Read', 'Write', 'Admin')");
                t.HasCheckConstraint("CK_access_requests_justification", "length(trim(justification)) > 0");
                t.HasCheckConstraint("CK_access_requests_version", "version >= 1");
                t.HasCheckConstraint("CK_access_requests_is_high_risk", "is_high_risk = (environment='Production' OR access_level='Admin')");
                t.HasCheckConstraint("CK_access_requests_high_risk_status", "is_high_risk = 1 OR status <> 'PendingSystemOwner'");
            });
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.HasOne(a => a.AccessRequest).WithMany(r => r.AuditEvents).HasForeignKey(a => a.AccessRequestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            
            b.ToTable(t => t.HasCheckConstraint("CK_audit_events_reason", "(event_type NOT IN ('ManagerRejected', 'OwnerRejected')) OR (reason IS NOT NULL AND length(trim(reason)) > 0)"));
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = ToSnakeCase(entity.GetTableName()!);
            entity.SetTableName(tableName);

            foreach (var property in entity.GetProperties())
            {
                var columnName = ToSnakeCase(property.Name);
                property.SetColumnName(columnName);
            }
            
            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var key in entity.GetForeignKeys())
            {
                key.SetConstraintName(ToSnakeCase(key.GetConstraintName()!));
            }
            
            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }
        var startUnderscores = Regex.Match(input, @"^_+");
        return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
    }
}

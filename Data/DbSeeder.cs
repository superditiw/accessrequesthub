using AccessRequestHub.Models;
using System;
using System.Linq;

namespace AccessRequestHub.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Users.Any()) return;

        var dana = new User { Id = Guid.NewGuid(), Email = "dana@example.com", DisplayName = "Dana", Role = UserRole.Employee };
        var carol = new User { Id = Guid.NewGuid(), Email = "carol@example.com", DisplayName = "Carol", Role = UserRole.Employee };
        var bob = new User { Id = Guid.NewGuid(), Email = "bob@example.com", DisplayName = "Bob", Role = UserRole.Employee };
        var alice = new User { Id = Guid.NewGuid(), Email = "alice@example.com", DisplayName = "Alice", Role = UserRole.Employee };
        var erin = new User { Id = Guid.NewGuid(), Email = "erin@example.com", DisplayName = "Erin", Role = UserRole.Auditor };

        alice.ManagerId = bob.Id;
        bob.ManagerId = carol.Id;
        carol.ManagerId = dana.Id;

        context.Users.AddRange(dana, carol, bob, alice, erin);
        context.SaveChanges();

        var crm = new Application { Id = Guid.NewGuid(), Name = "CRM", SystemOwnerId = carol.Id };
        var finance = new Application { Id = Guid.NewGuid(), Name = "Finance Portal", SystemOwnerId = dana.Id };

        context.Applications.AddRange(crm, finance);

        var v1 = new PolicyVersion { Code = "v1", EffectiveFrom = DateTime.UtcNow };
        context.PolicyVersions.Add(v1);

        context.SaveChanges();
    }
}

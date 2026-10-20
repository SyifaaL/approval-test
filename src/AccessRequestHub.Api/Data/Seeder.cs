using AccessRequestHub.Api.Data.Entities;

namespace AccessRequestHub.Api.Data;

public static class Seeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any())
        {
            return;
        }

        var alice = new User { Email = "alice@example.local", Name = "Alice" };
        var bob = new User { Email = "bob@example.local", Name = "Bob" };
        var carol = new User { Email = "carol@example.local", Name = "Carol" };
        var dana = new User { Email = "dana@example.local", Name = "Dana" };
        var erin = new User { Email = "erin@example.local", Name = "Erin", IsAuditor = true };

        db.Users.AddRange(alice, bob, carol, dana, erin);
        db.SaveChanges();

        alice.ManagerId = bob.Id;
        db.SaveChanges();

        var crm = new Application { Name = "CRM", OwnerUserId = carol.Id };
        var financePortal = new Application { Name = "Finance Portal", OwnerUserId = dana.Id };
        db.Applications.AddRange(crm, financePortal);

        db.PolicyVersions.Add(new PolicyVersion { Code = "v1", EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        db.SaveChanges();
    }
}

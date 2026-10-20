using AccessRequestHub.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Manager)
                .WithMany()
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Application>(e =>
        {
            e.HasIndex(a => a.Name).IsUnique();
            e.HasOne(a => a.OwnerUser)
                .WithMany()
                .HasForeignKey(a => a.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PolicyVersion>(e =>
        {
            e.HasKey(p => p.Code);
        });

        modelBuilder.Entity<AccessRequest>(e =>
        {
            e.HasIndex(r => new { r.RequesterId, r.ClientRequestId }).IsUnique();

            e.Property(r => r.Environment).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.AccessLevel).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);

            e.HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Application)
                .WithMany()
                .HasForeignKey(r => r.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<PolicyVersion>()
                .WithMany()
                .HasForeignKey(r => r.PolicyVersion)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint(
                "CK_AccessRequests_Environment",
                "Environment IN ('NonProduction', 'Production')"));
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AccessRequests_AccessLevel",
                "AccessLevel IN ('Read', 'Admin')"));
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AccessRequests_Status",
                "Status IN ('PendingManager', 'PendingSystemOwner', 'Approved', 'Rejected')"));
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AccessRequests_Justification_NotEmpty",
                "length(trim(Justification)) > 0"));
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AccessRequests_RejectionReason_RequiredWhenRejected",
                "Status <> 'Rejected' OR (RejectionReason IS NOT NULL AND length(trim(RejectionReason)) > 0)"));
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.HasIndex(a => new { a.RequestId, a.RequestVersionAfter }).IsUnique();

            e.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(32);

            e.HasOne(a => a.Request)
                .WithMany()
                .HasForeignKey(a => a.RequestId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.ActorUser)
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

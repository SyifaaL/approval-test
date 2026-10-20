using System.Net.Http.Json;
using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.Endpoints.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessRequestHub.Tests;

/// <summary>AuditEvents must be append-only even if application code has a bug: the database
/// itself (SQLite triggers from the InitialCreate migration) rejects UPDATE/DELETE.</summary>
public class AuditTriggerTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task UpdateAuditEvents_IsRejectedByDatabase()
    {
        var auditEventId = await CreateOneAuditEventAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AuditEvents SET Reason = 'tampered' WHERE Id = {auditEventId}"));
    }

    [Fact]
    public async Task DeleteAuditEvents_IsRejectedByDatabase()
    {
        var auditEventId = await CreateOneAuditEventAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM AuditEvents WHERE Id = {auditEventId}"));
    }

    private async Task<int> CreateOneAuditEventAsync()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "audit trigger test"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var detail = await alice.GetFromJsonAsync<AccessRequestDetailResponse>(
            $"/api/requests/{created!.Id}", JsonOptions);
        return detail!.AuditTrail.First().Id;
    }
}

using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.Endpoints.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Api.Endpoints;

public static class UserEndpoints
{
    /// <summary>Public endpoint for the demo user switcher only. Not a security boundary.</summary>
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/users", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users
                .OrderBy(u => u.Name)
                .Select(u => new UserResponse(u.Id, u.Email, u.Name))
                .ToListAsync(ct)));
    }
}

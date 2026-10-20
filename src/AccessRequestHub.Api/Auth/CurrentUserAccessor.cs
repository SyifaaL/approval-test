using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.Data.Entities;
using AccessRequestHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Api.Auth;

public interface ICurrentUserAccessor
{
    /// <summary>Resolves the calling user from the X-User-Email header. Throws UnauthenticatedException
    /// if the header is missing or does not match a known seeded user.</summary>
    Task<User> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}

public class CurrentUserAccessor(AppDbContext db) : ICurrentUserAccessor
{
    public const string HeaderName = "X-User-Email";

    public async Task<User> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            throw new UnauthenticatedException();
        }

        var email = values.ToString().Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        return user ?? throw new UnauthenticatedException();
    }
}

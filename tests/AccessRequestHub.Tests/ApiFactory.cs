using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AccessRequestHub.Tests;

/// <summary>One file-based SQLite database per test class (not in-memory, which disappears when
/// the connection closes), migrated and seeded via Program.cs's own startup code.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"arh-test-{Guid.NewGuid():n}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DbPath}");
    }

    public HttpClient CreateClientAs(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Email", email);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = DbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }
}

using System.Text;
using System.Text.Json;

namespace AccessRequestHub.Tests;

public abstract class TestBase(ApiFactory factory) : IClassFixture<ApiFactory>
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient As(string email) => Factory.CreateClientAs(email);

    protected HttpClient Anonymous() => Factory.CreateClient();

    protected static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

    protected static async Task<string?> GetProblemCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    protected static string NewClientRequestId([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():n}";
}

using System.Net;
using System.Net.Http.Json;
using AccessRequestHub.Api.Endpoints.Contracts;

namespace AccessRequestHub.Tests;

public class ConcurrencyTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Create_ConcurrentDuplicateClientRequestId_ResultsInSingleRow()
    {
        var clientRequestId = NewClientRequestId();
        var body = new CreateAccessRequestRequest(clientRequestId, "CRM", "NonProduction", "Read", "race test");

        var client1 = As("alice@example.local");
        var client2 = As("alice@example.local");

        var task1 = client1.PostAsJsonAsync("/api/requests", body, JsonOptions);
        var task2 = client2.PostAsJsonAsync("/api/requests", body, JsonOptions);
        var results = await Task.WhenAll(task1, task2);

        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));

        var ids = new List<Guid>();
        foreach (var r in results)
        {
            var parsed = await r.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
            ids.Add(parsed!.Id);
        }

        Assert.Equal(ids[0], ids[1]);

        var mine = await client1.GetFromJsonAsync<List<AccessRequestResponse>>("/api/requests/mine", JsonOptions);
        Assert.Single(mine!.Where(r => r.ClientRequestId == clientRequestId));
    }

    [Fact]
    public async Task Approve_ConcurrentSameVersion_OnlyOneSucceeds()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "concurrency"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob1 = As("bob@example.local");
        var bob2 = As("bob@example.local");

        var task1 = bob1.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        var task2 = bob2.PostAsync($"/api/requests/{created.Id}/reject", JsonBody(new DecisionRequest(1, "too risky")));
        var results = await Task.WhenAll(task1, task2);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);
    }
}

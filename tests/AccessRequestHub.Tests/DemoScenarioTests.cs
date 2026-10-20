using System.Net;
using System.Net.Http.Json;
using AccessRequestHub.Api.Endpoints.Contracts;

namespace AccessRequestHub.Tests;

/// <summary>The 7 required demo scenarios from the assessment brief, section "Required demo scenarios".</summary>
public class DemoScenarioTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Scenario1_Standard_NonProdRead_ApprovedAfterManager()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "standard access"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob = As("bob@example.local");
        var approve = await bob.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var result = await approve.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.Equal("Approved", result!.Status);
    }

    [Fact]
    public async Task Scenario2_Production_WaitsForSystemOwnerThenApproved()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "Production", "Read", "prod access"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob = As("bob@example.local");
        var afterManager = await bob.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        var afterManagerBody = await afterManager.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.Equal("PendingSystemOwner", afterManagerBody!.Status);

        // Dana (Finance Portal owner) is unrelated to this CRM request and must not be able to act on it.
        var dana = As("dana@example.local");
        var danaTry = await dana.PostAsync($"/api/requests/{created.Id}/approve", JsonBody(new DecisionRequest(2, null)));
        Assert.Equal(HttpStatusCode.NotFound, danaTry.StatusCode);

        var carol = As("carol@example.local");
        var afterOwner = await carol.PostAsync($"/api/requests/{created.Id}/approve", JsonBody(new DecisionRequest(2, null)));
        Assert.Equal(HttpStatusCode.OK, afterOwner.StatusCode);
        var finalBody = await afterOwner.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.Equal("Approved", finalBody!.Status);
    }

    [Fact]
    public async Task Scenario3_AdminAccess_WaitsForSystemOwner()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "Finance Portal", "NonProduction", "Admin", "admin access"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob = As("bob@example.local");
        var afterManager = await bob.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        var body = await afterManager.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        Assert.Equal("PendingSystemOwner", body!.Status);
        Assert.True(body.IsHighRisk);
    }

    [Fact]
    public async Task Scenario4_UnauthorizedApprover_IsRejectedByBackend()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        // Carol is CRM's owner (a real approver in the system) but not the approver for the
        // PendingManager stage this request is currently in.
        var carol = As("carol@example.local");
        var carolTry = await carol.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal(HttpStatusCode.Forbidden, carolTry.StatusCode);
    }

    [Fact]
    public async Task Scenario5_DuplicateSubmit_StaysAsOneRequest()
    {
        var alice = As("alice@example.local");
        var clientRequestId = NewClientRequestId();
        var body = new CreateAccessRequestRequest(clientRequestId, "CRM", "NonProduction", "Read", "duplicate test");

        var first = await alice.PostAsJsonAsync("/api/requests", body, JsonOptions);
        var second = await alice.PostAsJsonAsync("/api/requests", body, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        var secondBody = await second.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.Equal(firstBody!.Id, secondBody!.Id);

        var mine = await alice.GetFromJsonAsync<List<AccessRequestResponse>>("/api/requests/mine", JsonOptions);
        Assert.Single(mine!.Where(r => r.ClientRequestId == clientRequestId));
    }

    [Fact]
    public async Task Scenario6_ConcurrentAction_OnlyOneSucceeds()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "concurrency test"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob1 = As("bob@example.local");
        var bob2 = As("bob@example.local");

        var task1 = bob1.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        var task2 = bob2.PostAsync($"/api/requests/{created.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        var results = await Task.WhenAll(task1, task2);

        var statusCodes = results.Select(r => r.StatusCode).OrderBy(c => c).ToList();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], statusCodes);
    }

    [Fact]
    public async Task Scenario7_Rejected_StoresReasonAndIsTerminal()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob = As("bob@example.local");
        var reject = await bob.PostAsync($"/api/requests/{created!.Id}/reject", JsonBody(new DecisionRequest(1, "Not justified")));
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejected = await reject.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal("Not justified", rejected.RejectionReason);

        var detail = await alice.GetFromJsonAsync<AccessRequestDetailResponse>($"/api/requests/{created.Id}", JsonOptions);
        Assert.Contains(detail!.AuditTrail, e => e.EventType == "Rejected" && e.Reason == "Not justified");

        var reprocess = await bob.PostAsync($"/api/requests/{created.Id}/approve", JsonBody(new DecisionRequest(2, null)));
        Assert.Equal(HttpStatusCode.Conflict, reprocess.StatusCode);
        Assert.Equal("invalid_state", await GetProblemCodeAsync(reprocess));
    }
}

using System.Net;
using System.Net.Http.Json;
using AccessRequestHub.Api.Endpoints.Contracts;

namespace AccessRequestHub.Tests;

public class AuthorizationAndEdgeCaseTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Create_WithoutAuthHeader_Returns401()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ByUserWithoutManager_Returns422()
    {
        var bob = As("bob@example.local");
        var response = await bob.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "Finance Portal", "NonProduction", "Read", "x"),
            JsonOptions);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Create_BySystemOwner_ForOwnApplication_Returns422()
    {
        var carol = As("carol@example.local");
        var response = await carol.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameClientRequestId_DifferentPayload_Returns409IdempotencyMismatch()
    {
        var alice = As("alice@example.local");
        var clientRequestId = NewClientRequestId();

        var first = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(clientRequestId, "CRM", "NonProduction", "Read", "first payload"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(clientRequestId, "CRM", "Production", "Read", "different payload"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("idempotency_mismatch", await GetProblemCodeAsync(second));
    }

    [Fact]
    public async Task Approve_BySelf_Returns403()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var selfApprove = await alice.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);
    }

    [Fact]
    public async Task Approve_ByAuditor_Returns403()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var erin = As("erin@example.local");
        var erinTry = await erin.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal(HttpStatusCode.Forbidden, erinTry.StatusCode);
    }

    [Fact]
    public async Task Reject_WithoutReason_Returns422()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var bob = As("bob@example.local");
        var reject = await bob.PostAsync($"/api/requests/{created!.Id}/reject", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal((HttpStatusCode)422, reject.StatusCode);
    }

    [Fact]
    public async Task Approve_ByOwnerOfUnrelatedApplication_Returns404()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        // Dana owns Finance Portal, not CRM, and has no other relation to this request.
        var dana = As("dana@example.local");
        var danaTry = await dana.PostAsync($"/api/requests/{created!.Id}/approve", JsonBody(new DecisionRequest(1, null)));
        Assert.Equal(HttpStatusCode.NotFound, danaTry.StatusCode);
    }

    [Fact]
    public async Task GetDetail_ByUnrelatedUser_Returns404()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var dana = As("dana@example.local");
        var response = await dana.GetAsync($"/api/requests/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_ByAuditor_Returns200()
    {
        var alice = As("alice@example.local");
        var create = await alice.PostAsJsonAsync("/api/requests",
            new CreateAccessRequestRequest(NewClientRequestId(), "CRM", "NonProduction", "Read", "x"),
            JsonOptions);
        var created = await create.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);

        var erin = As("erin@example.local");
        var response = await erin.GetAsync($"/api/requests/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllRequests_ByNonAuditor_Returns403()
    {
        var alice = As("alice@example.local");
        var response = await alice.GetAsync("/api/requests");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllRequests_ByAuditor_Returns200()
    {
        var erin = As("erin@example.local");
        var response = await erin.GetAsync("/api/requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_IgnoresClientSuppliedIsHighRisk()
    {
        var alice = As("alice@example.local");
        // Raw JSON with an extra "isHighRisk": true that the request DTO does not expose;
        // the server must still compute IsHighRisk from Environment/AccessLevel only.
        var raw = $$"""
            {
                "clientRequestId": "{{NewClientRequestId()}}",
                "applicationName": "CRM",
                "environment": "NonProduction",
                "accessLevel": "Read",
                "justification": "trying to force high risk",
                "isHighRisk": true
            }
            """;
        var response = await alice.PostAsync("/api/requests",
            new StringContent(raw, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessRequestResponse>(JsonOptions);
        Assert.False(body!.IsHighRisk);
    }
}

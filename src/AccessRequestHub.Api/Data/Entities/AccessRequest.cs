using AccessRequestHub.Api.Domain;

namespace AccessRequestHub.Api.Data.Entities;

public class AccessRequest
{
    public Guid Id { get; set; }
    public required string ClientRequestId { get; set; }

    public int RequesterId { get; set; }
    public User? Requester { get; set; }

    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public RequestEnvironment Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public required string Justification { get; set; }

    public RequestStatus Status { get; set; }
    public required string PolicyVersion { get; set; }
    public bool IsHighRisk { get; set; }
    public string? RejectionReason { get; set; }

    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

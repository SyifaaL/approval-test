using AccessRequestHub.Api.Domain;

namespace AccessRequestHub.Api.Data.Entities;

public class AuditEvent
{
    public int Id { get; set; }

    public Guid RequestId { get; set; }
    public AccessRequest? Request { get; set; }

    public required string EventType { get; set; }

    public int ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }
    public string? Reason { get; set; }

    public int RequestVersionAfter { get; set; }
    public required string CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; }
}

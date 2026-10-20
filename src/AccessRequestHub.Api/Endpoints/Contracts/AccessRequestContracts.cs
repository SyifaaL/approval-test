namespace AccessRequestHub.Api.Endpoints.Contracts;

public record CreateAccessRequestRequest(
    string? ClientRequestId,
    string? ApplicationName,
    string? Environment,
    string? AccessLevel,
    string? Justification);

public record DecisionRequest(int? ExpectedVersion, string? Reason);

public record UserResponse(int Id, string Email, string Name);

public record AuditEventResponse(
    int Id,
    string EventType,
    string ActorEmail,
    string? FromStatus,
    string ToStatus,
    string? Reason,
    int RequestVersionAfter,
    string CorrelationId,
    DateTime OccurredAt);

public record AccessRequestResponse(
    Guid Id,
    string ClientRequestId,
    string RequesterEmail,
    string ApplicationName,
    string Environment,
    string AccessLevel,
    string Justification,
    string Status,
    string PolicyVersion,
    bool IsHighRisk,
    string? RejectionReason,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AccessRequestDetailResponse(
    Guid Id,
    string ClientRequestId,
    string RequesterEmail,
    string ApplicationName,
    string Environment,
    string AccessLevel,
    string Justification,
    string Status,
    string PolicyVersion,
    bool IsHighRisk,
    string? RejectionReason,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AuditEventResponse> AuditTrail);

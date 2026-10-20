namespace AccessRequestHub.Api.Endpoints.Contracts;

public record CreateAccessRequestRequest(
    string? ClientRequestId,
    string? ApplicationName,
    string? Environment,
    string? AccessLevel,
    string? Justification);

public record DecisionRequest(int? ExpectedVersion, string? Reason);

public record AccessRequestResponse(
    Guid Id,
    string ClientRequestId,
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

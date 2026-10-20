namespace AccessRequestHub.Api.Domain;

public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UnauthenticatedException()
    : DomainException("unauthenticated", "Missing or unknown X-User-Email header.");

public sealed class ResourceNotFoundException(string message)
    : DomainException("not_found", message);

public sealed class ForbiddenException(string message)
    : DomainException("forbidden", message);

public sealed class InvalidStateException(string message)
    : DomainException("invalid_state", message);

public sealed class ValidationException(string message)
    : DomainException("validation_error", message);

public sealed class VersionConflictException(string message)
    : DomainException("version_conflict", message);

public sealed class IdempotencyMismatchException(string message)
    : DomainException("idempotency_mismatch", message);

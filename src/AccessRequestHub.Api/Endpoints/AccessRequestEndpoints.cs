using AccessRequestHub.Api.Auth;
using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.Data.Entities;
using AccessRequestHub.Api.Domain;
using AccessRequestHub.Api.Endpoints.Contracts;
using AccessRequestHub.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Api.Endpoints;

public static class AccessRequestEndpoints
{
    public static void MapAccessRequestEndpoints(this WebApplication app)
    {
        app.MapPost("/api/requests", CreateAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateAccessRequestRequest request,
        HttpContext httpContext,
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken ct)
    {
        var actor = await currentUserAccessor.ResolveAsync(httpContext, ct);

        if (string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            throw new ValidationException("clientRequestId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new ValidationException("justification is required.");
        }

        if (!Enum.TryParse<RequestEnvironment>(request.Environment, ignoreCase: true, out var environment))
        {
            throw new ValidationException(
                $"environment must be one of: {string.Join(", ", Enum.GetNames<RequestEnvironment>())}.");
        }

        if (!Enum.TryParse<AccessLevel>(request.AccessLevel, ignoreCase: true, out var accessLevel))
        {
            throw new ValidationException(
                $"accessLevel must be one of: {string.Join(", ", Enum.GetNames<AccessLevel>())}.");
        }

        var application = string.IsNullOrWhiteSpace(request.ApplicationName)
            ? throw new ValidationException("applicationName is required.")
            : await db.Applications.FirstOrDefaultAsync(a => a.Name == request.ApplicationName, ct)
                ?? throw new ValidationException($"Unknown application: {request.ApplicationName}.");

        if (actor.ManagerId is null)
        {
            throw new ValidationException("Only users with an assigned manager may create access requests.");
        }

        if (application.OwnerUserId == actor.Id)
        {
            throw new ValidationException("You cannot request access to an application you own.");
        }

        var clientRequestId = request.ClientRequestId.Trim();
        var justification = request.Justification.Trim();
        var isHighRisk = ApprovalPolicy.IsHighRisk(ApprovalPolicy.CurrentPolicyVersion, environment, accessLevel);
        var now = DateTime.UtcNow;
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var cid)
            ? cid?.ToString() ?? string.Empty
            : string.Empty;

        var newRequest = new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = clientRequestId,
            RequesterId = actor.Id,
            ApplicationId = application.Id,
            Environment = environment,
            AccessLevel = accessLevel,
            Justification = justification,
            Status = RequestStatus.PendingManager,
            PolicyVersion = ApprovalPolicy.CurrentPolicyVersion,
            IsHighRisk = isHighRisk,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AccessRequests.Add(newRequest);
        db.AuditEvents.Add(new AuditEvent
        {
            RequestId = newRequest.Id,
            EventType = "Created",
            ActorUserId = actor.Id,
            FromStatus = null,
            ToStatus = RequestStatus.PendingManager,
            RequestVersionAfter = 1,
            CorrelationId = correlationId,
            OccurredAt = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/requests/{newRequest.Id}", ToResponse(newRequest, application));
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Another concurrent create with the same ClientRequestId won the race. Discard the
            // tracked (failed) insert and look up the row that actually landed, instead of a
            // read-then-write that would itself be racy.
            db.ChangeTracker.Clear();

            var existing = await db.AccessRequests
                .FirstOrDefaultAsync(r => r.RequesterId == actor.Id && r.ClientRequestId == clientRequestId, ct)
                ?? throw ex;

            var samePayload = existing.ApplicationId == application.Id
                && existing.Environment == environment
                && existing.AccessLevel == accessLevel
                && existing.Justification == justification;

            if (!samePayload)
            {
                throw new IdempotencyMismatchException(
                    "clientRequestId was already used to create a request with a different payload.");
            }

            return Results.Ok(ToResponse(existing, application));
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };

    private static AccessRequestResponse ToResponse(AccessRequest r, Application application) => new(
        r.Id,
        r.ClientRequestId,
        application.Name,
        r.Environment.ToString(),
        r.AccessLevel.ToString(),
        r.Justification,
        r.Status.ToString(),
        r.PolicyVersion,
        r.IsHighRisk,
        r.RejectionReason,
        r.Version,
        r.CreatedAt,
        r.UpdatedAt);
}

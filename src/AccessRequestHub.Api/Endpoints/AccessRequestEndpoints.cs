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
        app.MapPost("/api/requests/{id:guid}/approve", (Guid id, DecisionRequest body, HttpContext ctx, AppDbContext db, ICurrentUserAccessor cu, CancellationToken ct)
            => DecideAsync(id, body, isReject: false, ctx, db, cu, ct));
        app.MapPost("/api/requests/{id:guid}/reject", (Guid id, DecisionRequest body, HttpContext ctx, AppDbContext db, ICurrentUserAccessor cu, CancellationToken ct)
            => DecideAsync(id, body, isReject: true, ctx, db, cu, ct));
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

    private static async Task<IResult> DecideAsync(
        Guid id,
        DecisionRequest body,
        bool isReject,
        HttpContext httpContext,
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken ct)
    {
        var actor = await currentUserAccessor.ResolveAsync(httpContext, ct);

        var request = await db.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null || !ApprovalPolicy.IsRelatedParty(actor, request))
        {
            // Unrelated callers get 404 instead of 403 so request ids cannot be enumerated/probed.
            throw new ResourceNotFoundException("Access request not found.");
        }

        if (request.Status is RequestStatus.Approved or RequestStatus.Rejected)
        {
            throw new InvalidStateException($"Request is already {request.Status} and cannot be processed further.");
        }

        if (!ApprovalPolicy.CanDecide(actor, request))
        {
            throw new ForbiddenException("You are not the approver for this request's current stage.");
        }

        if (isReject && string.IsNullOrWhiteSpace(body.Reason))
        {
            throw new ValidationException("reason is required to reject a request.");
        }

        if (body.ExpectedVersion is null)
        {
            throw new ValidationException("expectedVersion is required.");
        }

        var currentStatus = request.Status;
        var nextStatus = isReject ? ApprovalPolicy.NextOnReject(request) : ApprovalPolicy.NextOnApprove(request);
        var rejectionReason = isReject ? body.Reason!.Trim() : null;
        var now = DateTime.UtcNow;
        var expectedVersion = body.ExpectedVersion.Value;
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var cid)
            ? cid?.ToString() ?? string.Empty
            : string.Empty;

        // Read + authorization happened above on plain (non-transactional) queries. The transaction
        // below exists only to make the conditional UPDATE and the audit insert atomic, and the
        // UPDATE is its first statement — reading inside the transaction first would upgrade a
        // read lock to a write lock mid-transaction, which causes spurious SQLITE_BUSY under
        // concurrent requests instead of a clean 409.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var affected = await db.AccessRequests
            .Where(r => r.Id == id && r.Version == expectedVersion && r.Status == currentStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, nextStatus)
                .SetProperty(r => r.Version, r => r.Version + 1)
                .SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.RejectionReason, rejectionReason), ct);

        if (affected == 0)
        {
            throw new VersionConflictException(
                "Request was modified by someone else since you loaded it. Refresh and try again.");
        }

        db.AuditEvents.Add(new AuditEvent
        {
            RequestId = id,
            EventType = isReject ? "Rejected" : "Approved",
            ActorUserId = actor.Id,
            FromStatus = currentStatus,
            ToStatus = nextStatus,
            Reason = rejectionReason,
            RequestVersionAfter = expectedVersion + 1,
            CorrelationId = correlationId,
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var updated = await db.AccessRequests.AsNoTracking().FirstAsync(r => r.Id == id, ct);
        return Results.Ok(ToResponse(updated, request.Application!));
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

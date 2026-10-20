using AccessRequestHub.Api.Data.Entities;

namespace AccessRequestHub.Api.Domain;

public static class ApprovalPolicy
{
    public const string CurrentPolicyVersion = "v1";

    public static bool IsHighRisk(string policyVersion, RequestEnvironment environment, AccessLevel accessLevel) =>
        policyVersion switch
        {
            "v1" => environment == RequestEnvironment.Production || accessLevel == AccessLevel.Admin,
            _ => throw new InvalidOperationException($"Unknown policy version: {policyVersion}")
        };

    public static RequestStatus NextOnApprove(AccessRequest request) =>
        request.Status switch
        {
            RequestStatus.PendingManager => request.IsHighRisk ? RequestStatus.PendingSystemOwner : RequestStatus.Approved,
            RequestStatus.PendingSystemOwner => RequestStatus.Approved,
            _ => throw new InvalidStateException($"Request is already {request.Status} and cannot be approved.")
        };

    public static RequestStatus NextOnReject(AccessRequest request) =>
        request.Status switch
        {
            RequestStatus.PendingManager or RequestStatus.PendingSystemOwner => RequestStatus.Rejected,
            _ => throw new InvalidStateException($"Request is already {request.Status} and cannot be rejected.")
        };

    /// <summary>Requires Requester and Application navigations to be loaded.</summary>
    public static int GetApproverUserId(AccessRequest request) =>
        request.Status switch
        {
            RequestStatus.PendingManager => request.Requester?.ManagerId
                ?? throw new InvalidOperationException("Requester has no manager; should have been rejected at create."),
            RequestStatus.PendingSystemOwner => request.Application?.OwnerUserId
                ?? throw new InvalidOperationException("Application navigation not loaded."),
            _ => throw new InvalidStateException($"Request in status {request.Status} has no pending approver.")
        };

    /// <summary>Whether actor may approve/reject this request right now. Requires Requester and Application navigations loaded.</summary>
    public static bool CanDecide(User actor, AccessRequest request)
    {
        if (request.Status is not (RequestStatus.PendingManager or RequestStatus.PendingSystemOwner))
        {
            return false;
        }

        if (actor.Id == request.RequesterId)
        {
            return false;
        }

        return actor.Id == GetApproverUserId(request);
    }

    /// <summary>Whether actor has a legitimate reason to read this request: requester, their manager,
    /// the application owner, or an auditor. Requires Requester and Application navigations loaded.</summary>
    public static bool IsRelatedParty(User actor, AccessRequest request) =>
        actor.IsAuditor
        || actor.Id == request.RequesterId
        || actor.Id == request.Requester?.ManagerId
        || actor.Id == request.Application?.OwnerUserId;
}

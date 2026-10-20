namespace AccessRequestHub.Api.Domain;

public enum RequestEnvironment
{
    NonProduction,
    Production
}

public enum AccessLevel
{
    Read,
    Admin
}

public enum RequestStatus
{
    PendingManager,
    PendingSystemOwner,
    Approved,
    Rejected
}

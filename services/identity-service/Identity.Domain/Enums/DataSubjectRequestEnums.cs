namespace Identity.Domain.Enums;

public enum DataSubjectRequestType
{
    Access = 1,
    Rectification = 2,
    Erasure = 3,
    Restriction = 4,
    Objection = 5
}

public enum DataSubjectRequestStatus
{
    Submitted = 1,
    IdentityVerified = 2,
    Approved = 3,
    Rejected = 4,
    Processing = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8
}

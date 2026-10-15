namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// The status for the Request SystemUser
/// When a Request is archived/softdeleted, the last current status is not changed
/// </summary>
public enum RequestStatus
{
    New,
    Accepted,
    Rejected,
    Denied,
    Timedout
}

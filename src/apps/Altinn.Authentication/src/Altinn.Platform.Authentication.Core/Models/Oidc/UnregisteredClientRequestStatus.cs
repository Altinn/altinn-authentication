namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Status for the unregistered client request
    /// </summary>
    public enum UnregisteredClientRequestStatus
    {
        Pending,
        Completed,
        Cancelled,
        Error
    }
}

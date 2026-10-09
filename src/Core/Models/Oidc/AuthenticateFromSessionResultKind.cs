namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public enum AuthenticateFromSessionResultKind
    {
        Success,          // success: code+state to downstream client
        NoValidSession     // OIDC error back to downstream client
    }
}

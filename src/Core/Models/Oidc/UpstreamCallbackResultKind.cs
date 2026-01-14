namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// indicates the kind of result from processing an upstream OIDC callback.
    /// </summary>
    public enum UpstreamCallbackResultKind
    {
        RedirectToClient,          // success: code+state to downstream client
        RedirectToGoTo,
        ErrorRedirectToClient,     // OIDC error back to downstream client
        LocalError                 // 4xx/5xx here
    }
}

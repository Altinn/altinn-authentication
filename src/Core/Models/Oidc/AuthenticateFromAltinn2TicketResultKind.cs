namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public enum AuthenticateFromAltinn2TicketResultKind
    {
        Success,          // success: code+state to downstream client
        NoValidSession     // OIDC error back to downstream client
    }
}

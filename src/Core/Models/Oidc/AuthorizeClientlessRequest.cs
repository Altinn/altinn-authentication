namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defined a open authorize request without client_id. Typical requested by Altinn Apps or other applications in Altinn Platform that do not have a client_id.
    /// </summary>
    public sealed class AuthorizeClientlessRequest
    {
       public string GoTo { get; init; }

        public string RequestedIss { get; init; }
    }
}

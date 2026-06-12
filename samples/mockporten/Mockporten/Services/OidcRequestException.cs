using System;

namespace Mockporten.Services
{
    /// <summary>
    /// Thrown when an OIDC request fails validation (PKCE mismatch, invalid or
    /// expired authorization code / request object, wrong token use). Carries an
    /// OAuth 2.0 error code so the controller can render the standard response.
    /// </summary>
    public class OidcRequestException : Exception
    {
        public string Error { get; }

        public OidcRequestException(string error, string description)
            : base(description)
        {
            Error = error;
        }
    }
}

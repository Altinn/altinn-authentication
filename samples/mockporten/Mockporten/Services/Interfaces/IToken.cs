using Mockporten.Models;
using Mockporten.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockporten.Services.Interface
{
    public interface IToken
    {
        Task<string> GetAuthorizationCode(OidcAuthorizationModel oidcAuthorizationModel);

        /// <summary>
        /// Validates the authorization code and PKCE, then issues a token.
        /// Throws <see cref="OidcRequestException"/> on any failure.
        /// </summary>
        Task<string> GetTokenFromCode(string code, string codeVerifier);

        /// <summary>
        /// Stateless PAR: serializes the authorization request into a signed,
        /// short-lived JWT and returns it (the value to embed in request_uri).
        /// </summary>
        Task<string> CreateRequestObject(OidcAuthorizationModel oidcAuthorizationModel);

        /// <summary>
        /// Validates a request_uri value and rehydrates the authorization
        /// request. Throws <see cref="OidcRequestException"/> if invalid.
        /// </summary>
        Task<OidcAuthorizationModel> ReadRequestObject(string requestUri);
    }
}

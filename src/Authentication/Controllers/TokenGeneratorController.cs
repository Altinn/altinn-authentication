#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Integration.Clients;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Authentication.Controllers;

/// <summary>
/// Controller for generating test tokens
/// </summary>
/// <param name="generalSettings">Settings</param>
/// <param name="registerService">Register service</param>
/// <param name="tokenService">Token service</param>
[Route("authentication/api/v1/tokengenerator/")]
[ApiController]
public class TokenGeneratorController(IOptions<GeneralSettings> generalSettings, RegisterService registerService, ITokenService tokenService) : ControllerBase
{
    private readonly GeneralSettings options = generalSettings.Value;
    private static readonly List<string> DEFAULTSCOPES = ["altinn:portal/enduser"];

    /// <summary>
    /// Generates a personal access token for a user identified by the provided pid.
    /// </summary>
    /// <param name="pid">Person identifier</param>
    /// <param name="scopes">Collection of scopes to be included in the token. Default: altinn:portal/enduser</param>
    /// <param name="authLvl">Authentication level to be used on in the token. Default: 3 (idporten-loa-substantial)</param>
    /// <param name="authMethod">Authentication method to be specified in the token. Default: AltinnAuthenticationTokenGenerator</param>
    /// <param name="ttl">Time to live to be used in the generated token. Default: 1800 (seconds)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A personal access token for the user</returns>
    [HttpGet()]
    [Route("personaltoken")]
    public async Task<ActionResult<string>> PersonalToken([FromQuery] string pid, [FromQuery] IEnumerable<string>? scopes = null, [FromQuery] string authLvl = "3", [FromQuery] string authMethod = "AltinnAuthenticationTokenGenerator", [FromQuery] uint ttl = 1800, CancellationToken cancellationToken = default)
    {
        var partyinfo = await registerService.GetParty(pid, cancellationToken);
        if (!partyinfo.Success || partyinfo.Party == null)
        {
            return NotFound($"No party found for pid {pid}");
        }

        return Ok(await GenerateToken(partyinfo.Party, scopes ?? DEFAULTSCOPES, authLvl, authMethod, ttl));
    }

    private async Task<string> GenerateToken(PartyInfo party, IEnumerable<string> scopes, string authLvl, string authMethod, uint ttl)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, party.User.UserId.ToString(), ClaimValueTypes.String, options.AltinnOidcIssuerUrl),
            new Claim(AltinnCoreClaimTypes.UserId, party.User.UserId.ToString(), ClaimValueTypes.String, options.AltinnOidcIssuerUrl),
            new Claim(AltinnCoreClaimTypes.PartyUUID, party.Uuid.ToString(), ClaimValueTypes.Integer32, options.AltinnOidcIssuerUrl),
            new Claim(AltinnCoreClaimTypes.PartyID, party.PartyId.ToString(), ClaimValueTypes.Integer32, options.AltinnOidcIssuerUrl),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authMethod, ClaimValueTypes.String, options.AltinnOidcIssuerUrl),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authLvl, ClaimValueTypes.Integer32, options.AltinnOidcIssuerUrl),
        ];

        if (!string.IsNullOrWhiteSpace(party.User.Username))
        {
            claims.Add(new Claim(AltinnCoreClaimTypes.UserName, party.User.Username, ClaimValueTypes.String, options.AltinnOidcIssuerUrl));
        }

        foreach (string scope in scopes)
        {
            claims.Add(new Claim(AuthzConstants.CLAIM_SCOPE, scope, ClaimValueTypes.String, options.AltinnOidcIssuerUrl));
        }

        claims.Add(new Claim("iss", options.AltinnOidcIssuerUrl, ClaimValueTypes.String, options.AltinnOidcIssuerUrl));
        claims.Add(new Claim("jti", Guid.NewGuid().ToString(), ClaimValueTypes.String, options.AltinnOidcIssuerUrl));

        ClaimsIdentity identity = new ClaimsIdentity();
        identity.AddClaims(claims);
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);

        return await tokenService.GenerateToken(principal);
    }
}
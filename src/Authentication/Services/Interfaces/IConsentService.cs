using System;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces;

/// <summary>
/// The Service that support the API of consent requests
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Get the redirect URL for a consent request
    /// 
    /// </summary>
    /// <param name="requestId">The consent request Id as a UUID</param>
    /// <returns>The redirect url</returns>
    Task<string> GetConsentRequestRedirectUrl(Guid requestId);
}
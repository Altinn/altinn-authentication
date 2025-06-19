#nullable enable

using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services;

/// <inheritdoc/>
public class ConsentService(IAccessManagementClient accessManagementClient) : IConsentService
{
    /// <inheritdoc/>
    public async Task<string> GetConsentRequestRedirectUrl(Guid requestId)
    {
        ConsentRedirectUrl? redirectUrl = await accessManagementClient.GetConsentRequestRedirectUrl(requestId);
        return redirectUrl?.Url ?? string.Empty;
    }
}
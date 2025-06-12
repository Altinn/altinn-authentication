using System.Reflection;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;

public static class ApiEndpointExtensions
{
    public static string? Url(this Endpoints endpoint)
    {
        return endpoint.GetType()
                   .GetField(endpoint.ToString())?
                   .GetCustomAttribute<EndpointInfoAttribute>()?.Url 
               ?? string.Empty;
    }
}
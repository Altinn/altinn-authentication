using System.Reflection;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public static class ApiEndpointExtensions
{
    public static string Url(this ApiEndpoints endpoint)
    {
        return endpoint.GetType()
                   .GetField(endpoint.ToString())?
                   .GetCustomAttribute<EndpointInfoAttribute>()?.Url 
               ?? string.Empty;
    }
}
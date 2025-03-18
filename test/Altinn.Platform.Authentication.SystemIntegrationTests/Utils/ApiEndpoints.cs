using System.Reflection;
using System.Text.RegularExpressions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public enum ApiEndpoints
{
    // System Register Endpoints
    [EndpointInfo("v1/systemregister", "GET")]
    GetAllSystemsFromRegister,

    [EndpointInfo("v1/systemregister/{systemId}", "GET")]
    GetSystemRegisterById,

    [EndpointInfo("v1/systemregister/vendor/{systemId}", "GET")]
    GetVendorSystemRegisterById,

    [EndpointInfo("v1/systemregister/vendor/{systemId}", "PUT")]
    UpdateVendorSystemRegister,

    [EndpointInfo("v1/systemregister/vendor/{systemId}/rights", "PUT")]
    UpdateRightsVendorSystemRegister,

    [EndpointInfo("v1/systemregister/vendor/{systemId}", "DELETE")]
    DeleteSystemSystemRegister,

    [EndpointInfo("v1/systemregister/{systemId}/rights", "GET")]
    GetSystemRegisterRights,

    [EndpointInfo("v1/systemregister/vendor", "POST")]
    CreateSystemRegister,

    // System User Endpoints
    [EndpointInfo("v1/systemuser/{party}", "GET")]
    GetSystemUsersByParty,

    [EndpointInfo("v1/systemuser/{party}", "POST")]
    CreateSystemUser,

    [EndpointInfo("v1/systemuser/{party}/{systemUserId}", "GET")]
    GetSystemUserById,

    [EndpointInfo("v1/systemuser/{party}/{systemUserId}", "DELETE")]
    DeleteSystemUserById,
    
    [EndpointInfo("v1/systemuser/byExternalId", "GET")]
    GetSystemUserByExternalId,

    [EndpointInfo("v1/systemuser", "PUT")] 
    UpdateSystemUser,

    [EndpointInfo("v1/systemuser/vendor/bysystem/{systemId}", "GET")]
    GetSystemUsersBySystemForVendor,

    // System User Request Endpoints
    [EndpointInfo("v1/systemuser/request/vendor", "POST")]
    CreateSystemUserRequest,

    [EndpointInfo("v1/systemuser/request/vendor/{requestId}", "GET")]
    GetSystemUserRequestStatus,

    [EndpointInfo("v1/systemuser/request/vendor/{requestId}", "DELETE")]
    DeleteSystemUserRequest,

    [EndpointInfo("v1/systemuser/request/vendor/byexternalref/{systemId}/{orgNo}/{externalRef}", "GET")]
    GetSystemUserRequestByExternalRef,

    [EndpointInfo("v1/systemuser/request/{party}/{requestId}", "GET")]
    GetSystemUserRequestByParty,

    [EndpointInfo("v1/systemuser/request/{party}/{requestId}/approve", "POST")]
    ApproveSystemUserRequest,

    [EndpointInfo("v1/systemuser/request/vendor/bysystem/{systemId}", "GET")]
    GetSystemUserRequestsBySystem,

    [EndpointInfo("v1/systemuser/request/{party}/{requestId}/reject", "POST")]
    RejectSystemUserRequest,

    //Change request endpoints
    [EndpointInfo("v1/systemuser/changerequest/vendor", "POST")]
    PostChangeRequestVendor,
    
    [EndpointInfo("v1/systemuser/changerequest/vendor/{requestId}", "GET")]
    GetChangeRequestByRequestId,

    [EndpointInfo("v1/systemuser/changerequest/vendor/byexternalref/{systemId}/{vendor}/{externalRef}", "GET")]
    GetChangeRequestByExternalRef,

    [EndpointInfo("v1/systemuser/changerequest/vendor/{requestId}", "GET")]
    GetChangeRequestByRequestIdUrl,
    
    [EndpointInfo("v1/systemuser/changerequest/{partyId}/{requestId}/approve", "POST")]
    ApproveChangeRequest,
    
    // <!--- CLIENT DELEGATION ENDPOINTS -->
    [EndpointInfo("v1/systemuser/request/vendor/agent", "POST")]
    PostAgentClientRequest,
    
    [EndpointInfo("v1/systemuser/request/vendor/agent/{requestId}", "GET")]
    GetVendorAgentRequestById, // Retrieves a specific vendor agent request by request ID

    [EndpointInfo("v1/systemuser/request/vendor/agent/byexternalref/{systemId}/{orgNo}/{externalRef}", "GET")]
    GetVendorAgentRequestByExternalRef, // Retrieves a vendor agent request using an external reference

    [EndpointInfo("v1/systemuser/request/agent/{party}/{requestId}", "GET")]
    GetAgentRequestById, // Retrieves a specific agent request by party and request ID (BFF)

    [EndpointInfo("v1/systemuser/request/agent/{party}/{requestId}/approve", "POST")]
    ApproveAgentRequest, // Approves an agent request for a specific party

    [EndpointInfo("v1/systemuser/request/vendor/agent/bysystem/{systemId}", "GET")]
    GetVendorAgentRequestsBySystemId, // Retrieves all vendor agent requests associated with a system ID

    [EndpointInfo("v1/systemuser/request/agent/{party}/{requestId}/reject", "POST")]
    RejectAgentRequest // Rejects an agent request for a specific party
}

[AttributeUsage(AttributeTargets.Field)]
public class EndpointInfoAttribute : Attribute
{
    public string Url { get; }
    public HttpMethod Method { get; }

    public EndpointInfoAttribute(string url, string method)
    {
        Url = url;
        Method = new HttpMethod(method.ToUpper()); // ✅ Normalize method case
    }
}

public static class ApiEndpointHelper
{
    public static List<ApiEndpoint> GetEndpoints()
    {
        return Enum.GetValues<ApiEndpoints>()
            .Select(e => new ApiEndpoint(GetUrl(e), GetMethod(e)))
            .ToList();
    }

    public static string GetUrl(ApiEndpoints endpoint)
    {
        return endpoint.GetType()
                   .GetField(endpoint.ToString())
                   ?.GetCustomAttribute<EndpointInfoAttribute>()?.Url
               ?? string.Empty;
    }

    private static HttpMethod GetMethod(ApiEndpoints endpoint)
    {
        return endpoint.GetType()
            .GetField(endpoint.ToString())!
            .GetCustomAttribute<EndpointInfoAttribute>()?.Method ?? HttpMethod.Get;
    }
}

public class ApiEndpoint
{
    public string Url { get; }
    public HttpMethod Method { get; }

    public ApiEndpoint(string url, HttpMethod method)
    {
        Url = NormalizeUrl(url);
        Method = method;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ApiEndpoint other) return false;

        return Url.Equals(other.Url, StringComparison.OrdinalIgnoreCase) &&
               Method.Method.Equals(other.Method.Method, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Url.ToLowerInvariant(), Method.Method.ToLowerInvariant());
    }

    /// <summary>
    /// Normalizes the URL to ensure Swagger and test paths match.
    /// </summary>
    public static string NormalizeUrl(string url)
    {
        if (!url.StartsWith("v1/")) url = "v1/" + url; // ✅ Ensure v1 prefix
        // ✅ Remove trailing slashes for consistency
        url = url.TrimEnd('/');
        return url;
    }
}
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class TestState
{
    private string? Token { get; set; }
    public string? VendorId { get; set; }
    
    public string? AccessPackage { get; set; }
    public string? Name { get; private set; }
    public string? ClientId { get; private set; }
    public string SystemId => $"{VendorId}_{Name}"; // Combination of vendorId and randomNames
    public string? RedirectUrl { get; private set; }
    
    public List<string> AllowedRedirectUrls { get; private set; } = new();

    public string? ExternalRef { get; set; }

    private readonly string _templateContent;

    public List<Right> Rights { get; set; } = [];

    public TestState(string filepath)
    {
        _templateContent = File.Exists(filepath)
            ? File.ReadAllText(filepath)
            : throw new FileNotFoundException($"Template file not found: {filepath}");
        Name = Guid.NewGuid().ToString();
    }

    public TestState WithVendor(string? vendorId)
    {
        VendorId = vendorId;
        return this;
    }
    
    public TestState WithAccessPackage(string? accessPackageId)
    {
        AccessPackage = accessPackageId;
        return this;
    }

    public TestState WithClientId(string clientId)
    {
        ClientId = clientId;
        return this;
    }

    public TestState WithToken(string? token)
    {
        Token = token;
        return this;
    }

    public TestState WithExternalRef(string externalRef)
    {
        ExternalRef = externalRef;
        return this;
    }
    
    public TestState WithRedirectUrl(string? redirectUrl)
    {
        RedirectUrl = redirectUrl;
        return this;
    }
    
    public TestState WithAllowedRedirectUrls(params string[] urls)
    {
        if (urls == null || urls.Length == 0)
        {
            throw new ArgumentException("At least one URL must be provided.", nameof(urls));
        }

        AllowedRedirectUrls.AddRange(urls);
        return this;
    }
    
    public TestState WithName(string name)
    {
        Name = name;
        return this;
    }

    public TestState WithResource(string value, string id)
    {
        var resource = new Resource
        {
            Id = id,
            Value = value
        };

        var right = new Right();
        right.Resource.Add(resource);

        Rights.Add(right);
        return this;
    }

    public string GenerateRequestBody()
    {
        // Perform placeholder replacements
        var requestBody = _templateContent
            .Replace("{vendorId}", VendorId ?? string.Empty)
            .Replace("{Name}", Name ?? string.Empty)
            .Replace("{clientId}", ClientId ?? string.Empty)
            .Replace("{redirectUrl}", RedirectUrl)
            .Replace("{token}", Token ?? string.Empty)
            .Replace("{accessPackage}", AccessPackage ?? string.Empty)
            ;

        var rightsJson = JsonSerializer.Serialize(Rights, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        
        // Serialize Allowed Redirect URLs
        var allowedRedirectUrlsJson = AllowedRedirectUrls.Count != 0 
            ? JsonSerializer.Serialize(AllowedRedirectUrls, new JsonSerializerOptions
            {
                WriteIndented = false
            })
            : "[]"; // Default empty array if no URLs are provided

        requestBody = requestBody.Replace("{allowedRedirectUrls}", $"\"allowedRedirectUrls\": {allowedRedirectUrlsJson}");

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Validate JSON structure
        try
        {
            using var doc = JsonDocument.Parse(finalJson);
            return finalJson;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Generated JSON is invalid.", ex);
        }
    }
    
}
namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class Right
{
    public List<Resource>? Resource { get; set; }
}

public class Resource
{
    public string? Value { get; set; }
    public string? Id { get; set; }
}
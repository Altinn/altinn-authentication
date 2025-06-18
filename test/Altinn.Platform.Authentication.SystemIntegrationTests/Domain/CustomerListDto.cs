namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class CustomerListDto
{
    public string? id { get; set; }
    public string? name { get; set; }
    public string? orgNo { get; set; }
    public List<Access>? access { get; set; }
}

public class Access
{
    public string? role { get; set; }
    public List<string>? packages { get; set; }
}
namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.Testdata;

public class ExternalRole
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public bool IsKeyRole { get; set; }
    public string Urn { get; set; }
    
    public List<AccessPackage>? Packages { get; set; } // enrichment

}

public class AccessPackage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Urn { get; set; }
    public string Description { get; set; }
    public bool IsDelegable { get; set; }
    public bool IsAssignable { get; set; }
    public string Area { get; set; }
    public object Resources { get; set; }
}
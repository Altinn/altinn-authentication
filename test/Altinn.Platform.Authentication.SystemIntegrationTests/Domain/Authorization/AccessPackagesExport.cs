namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;

public class AccessPackagesExport
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Urn { get; set; }
    public string Description { get; set; } = default!;
    public string Type { get; set; } = default!;
    public List<AccessPackageAreaDto> Areas { get; set; } = new();
}

public class AccessPackageAreaDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Urn { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Icon { get; set; } = default!;
    public List<AccessPackageDto> Packages { get; set; } = new();
}

public class AccessPackageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Urn { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsDelegable { get; set; }
    public bool IsAssignable { get; set; }
    public object? Area { get; set; }  // Null i eksempelet – spesifiser evt. riktig type
    public object? Resources { get; set; }  // Null i eksempelet – spesifiser evt. riktig type
}
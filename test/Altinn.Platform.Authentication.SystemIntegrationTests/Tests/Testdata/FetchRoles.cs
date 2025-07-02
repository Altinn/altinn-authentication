using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.Testdata;

public class FetchRoles
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITestOutputHelper _outputHelper;

    public FetchRoles(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    private const string RolesEndpoint = "https://platform.at22.altinn.cloud/accessmanagement/api/v1/meta/info/roles";

    private static string GetPackagesPerRoleEndpoint(string roleId) =>
        $"https://platform.at22.altinn.cloud/accessmanagement/api/v1/meta/info/roles/{roleId}/packages";

    [Fact]
    public async Task FrontendNormalizedOutput()
    {
        using var client = new HttpClient();

        List<ExternalRole>? roles = await client.GetFromJsonAsync<List<ExternalRole>>(RolesEndpoint, _options);
        Assert.NotNull(roles);

        Dictionary<string, PackageShort> allPackages = new();
        List<NormalizedRole> normalizedRoles = [];

        foreach (var role in roles)
        {
            var packagesUrl = GetPackagesPerRoleEndpoint(role.Id.ToString());

            try
            {
                var response = await client.GetAsync(packagesUrl);
                if (!response.IsSuccessStatusCode) continue;

                List<PackageWrapper>? wrappers = await response.Content.ReadFromJsonAsync<List<PackageWrapper>>(_options);
                if (wrappers == null || wrappers.Count == 0) continue;

                var packageUrns = new List<string>();

                foreach (var wrapper in wrappers.Where(w => w.Package != null))
                {
                    var pkg = wrapper.Package;
                    if (!allPackages.ContainsKey(pkg.Urn))
                    {
                        allPackages[pkg.Urn] = new PackageShort
                        {
                            Name = pkg.Name,
                            Description = pkg.Description,
                            Urn = pkg.Urn,
                            IsDelegable = pkg.IsDelegable,
                            IsAssignable = pkg.IsAssignable
                        };
                    }

                    packageUrns.Add(pkg.Urn);
                }

                normalizedRoles.Add(new NormalizedRole
                {
                    RoleId = role.Id.ToString(),
                    RoleName = role.Name,
                    Description = role.Description,
                    PackageUrns = packageUrns
                });
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"❌ Feil for rolle {role.Name}: {ex.Message}");
            }
        }

        // Serialize roles
        string rolesJson = JsonSerializer.Serialize(normalizedRoles, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        string rolesPath = Path.Combine(Directory.GetCurrentDirectory(), "roles.json");
        await File.WriteAllTextAsync(rolesPath, rolesJson);

        // Serialize packages
        string packagesJson = JsonSerializer.Serialize(allPackages.Values.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        string packagesPath = Path.Combine(Directory.GetCurrentDirectory(), "packages.json");
        await File.WriteAllTextAsync(packagesPath, packagesJson);

        _outputHelper.WriteLine($"✅ Skrev ut {normalizedRoles.Count} roller til {rolesPath}");
        _outputHelper.WriteLine($"✅ Skrev ut {allPackages.Count} unike pakker til {packagesPath}");
    }

    // --- Helper classes ---

    public class PackageWrapper
    {
        public string Id { get; set; }
        public ExternalRole Role { get; set; }
        public AccessPackage Package { get; set; }
        public EntityVariant EntityVariant { get; set; }
        public bool HasAccess { get; set; }
        public bool CanDelegate { get; set; }
    }

    public class EntityVariant
    {
        public string Id { get; set; }
        public string TypeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class PackageShort
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Urn { get; set; }
        public bool IsDelegable { get; set; }
        public bool IsAssignable { get; set; }
    }

    public class NormalizedRole
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
        public List<string> PackageUrns { get; set; } = new();
    }

    public class ExternalRole
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class AccessPackage
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Urn { get; set; }
        public bool IsDelegable { get; set; }
        public bool IsAssignable { get; set; }
    }
}
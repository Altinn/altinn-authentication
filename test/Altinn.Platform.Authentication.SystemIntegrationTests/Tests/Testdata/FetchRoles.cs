using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.Testdata;

public class FetchRoles
{
    JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITestOutputHelper _outputHelper;

    public FetchRoles(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    const string rolesEndpoint = "https://platform.at22.altinn.cloud/accessmanagement/api/v1/meta/info/roles";

    string GetPackagesPerRoleEndpoint(string roleId)
    {
        return $"https://platform.at22.altinn.cloud/accessmanagement/api/v1/meta/info/roles/{roleId}/packages";
    }

    [Fact]
    public async Task FetchRolesTest()
    {
        using var client = new HttpClient();
        List<ExternalRole>? roles = await client.GetFromJsonAsync<List<ExternalRole>>(rolesEndpoint, options);
        Assert.NotNull(roles);

        //Fetch packages per role (static Id just to test)
        var packages = GetPackagesPerRoleEndpoint("348b2f47-47ee-4084-abf8-68aa54c2b27f");
        var resp = await client.GetAsync(packages);
        _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FetchRolesGptTest()
    {
        using var client = new HttpClient();
        List<ExternalRole>? roles = await client.GetFromJsonAsync<List<ExternalRole>>(rolesEndpoint, options);
        Assert.NotNull(roles);

        foreach (var role in roles)
        {
            var packagesUrl = GetPackagesPerRoleEndpoint(role.Id.ToString());
            try
            {
                var response = await client.GetAsync(packagesUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _outputHelper.WriteLine($"❌ Failed for {role.Name} ({role.Id}) — {response.StatusCode}");
                    continue;
                }

                List<PackageWrapper>? packages = await response.Content.ReadFromJsonAsync<List<PackageWrapper>>(options);

                // Map AccessPackage from wrapper
                role.Packages = packages?
                    .Where(p => p.Package != null)
                    .Select(p => p.Package!)
                    .ToList();
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"⚠️ Error for {role.Name}: {ex.Message}");
            }
        }

        // Optional: print enriched result
        foreach (var role in roles)
        {
            _outputHelper.WriteLine($"🟢 {role.Name} ({role.Id}) — {role.Packages?.Count ?? 0} packages");
            if (role.Packages != null)
            {
                foreach (var p in role.Packages)
                {
                    _outputHelper.WriteLine($"   📦 {p.Name} (Delegable: {p.IsDelegable}, Assignable: {p.IsAssignable})");
                }
            }
        }
    }

    // Denne brukes til å generere "ny backend" for frontend fra de to endepunktene. 
    [Fact]
    public async Task FrontendOutputTest()
    {
        using var client = new HttpClient();
        List<ExternalRole>? roles = await client.GetFromJsonAsync<List<ExternalRole>>(rolesEndpoint, options);
        Assert.NotNull(roles);

        List<RoleWithPackagesFrontend> frontendList = [];

        foreach (var role in roles)
        {
            var packagesUrl = GetPackagesPerRoleEndpoint(role.Id.ToString());
            try
            {
                var response = await client.GetAsync(packagesUrl);
                if (!response.IsSuccessStatusCode) continue;

                List<PackageWrapper>? wrappers = await response.Content.ReadFromJsonAsync<List<PackageWrapper>>(options);
                if (wrappers == null || wrappers.Count == 0) continue;

                var frontendDto = new RoleWithPackagesFrontend
                {
                    RoleId = role.Id.ToString(),
                    RoleName = role.Name,
                    Description = role.Description,
                    Packages = wrappers
                        .Where(w => w.Package != null)
                        .Select(w => new PackageShort
                        {
                            Name = w.Package.Name,
                            Description = w.Package.Description,
                            Urn = w.Package.Urn,
                            IsDelegable = w.Package.IsDelegable,
                            IsAssignable = w.Package.IsAssignable
                        })
                        .ToList()
                };

                frontendList.Add(frontendDto);
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"Feil for rolle {role.Name}: {ex.Message}");
            }
        }

        // 💾 Lagre som JSON
        var json = JsonSerializer.Serialize(frontendList, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "roles_with_packages_frontend.json");
        await File.WriteAllTextAsync(outputPath, json);

        _outputHelper.WriteLine($"✅ Skrev ut til {outputPath}");
    }

// Helper class to deserialize package-per-role response
    public class PackageWrapper
    {
        public string Id { get; set; }
        public ExternalRole Role { get; set; }
        public AccessPackage Package { get; set; }
        public EntityVariant EntityVariant { get; set; } // <-- was string before
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

    public class RoleWithPackagesFrontend
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
        public int PackageCount => Packages?.Count ?? 0;
        public List<PackageShort> Packages { get; set; } = new();
    }

    public class PackageShort
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Urn { get; set; }
        public bool IsDelegable { get; set; }
        public bool IsAssignable { get; set; }
    }
}
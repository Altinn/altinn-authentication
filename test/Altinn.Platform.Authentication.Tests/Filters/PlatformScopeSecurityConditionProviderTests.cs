#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Swashbuckle.Security;
using Altinn.Platform.Authentication.Core.Authorization;
using Altinn.Platform.Authentication.Filters;
using Altinn.Swashbuckle.Security;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Filters;

/// <summary>
/// The shared Altinn conventions only derive scopes from <c>RequireScopeAnyOf</c>. This service
/// also has a policy built on the PEP library's scope requirement, whose scopes would otherwise
/// be enforced but left undocumented.
/// </summary>
public class PlatformScopeSecurityConditionProviderTests
{
    private const string SchemeName = "altinn";

    [Fact]
    public async Task EmitsOneConditionPerScope_UnderTheOidcScheme()
    {
        InternalScopeOrAccessTokenRequirement requirement = new("altinn:authentication/systemuser.admin");

        List<SecurityRequirementCondition> conditions = await ConditionsFor(requirement, enabled: true);

        SecurityRequirementCondition condition = Assert.Single(conditions);
        Assert.Equal(SchemeName, condition.SchemeName);
        Assert.Equal("altinn:authentication/systemuser.admin", condition.Scope);
    }

    [Fact]
    public async Task EmitsAConditionPerScope_WhenTheRequirementAcceptsSeveral()
    {
        InternalScopeOrAccessTokenRequirement requirement = new("scope:one")
        {
            Scope = ["scope:one", "scope:two"],
        };

        List<SecurityRequirementCondition> conditions = await ConditionsFor(requirement, enabled: true);

        Assert.Equal(["scope:one", "scope:two"], conditions.Select(c => c.Scope).Order());
    }

    [Fact]
    public async Task EmitsNothing_WhenTheOidcSchemeIsDisabled()
    {
        // With no scheme to hang them on there is nothing to say, and emitting a requirement
        // that references an undeclared scheme would produce an invalid document.
        InternalScopeOrAccessTokenRequirement requirement = new("altinn:authentication/systemuser.admin");

        List<SecurityRequirementCondition> conditions = await ConditionsFor(requirement, enabled: false);

        Assert.Empty(conditions);
    }

    private static async Task<List<SecurityRequirementCondition>> ConditionsFor(
        InternalScopeOrAccessTokenRequirement requirement,
        bool enabled)
    {
        AltinnSecurityOptions options = new()
        {
            EnableAltinnOidcScheme = enabled,
            AltinnOidcSchemeName = SchemeName,
        };

        Mock<IOptionsMonitor<AltinnSecurityOptions>> monitor = new();
        monitor.Setup(m => m.Get(It.IsAny<string>())).Returns(options);
        monitor.SetupGet(m => m.CurrentValue).Returns(options);

        IOpenApiAuthorizationRequirementConditionProvider provider =
            new PlatformScopeSecurityConditionProvider(monitor.Object);

        OpenApiSecurityContext context = new() { DocumentName = ApiDocuments.External };

        List<SecurityRequirementCondition> conditions = [];
        await foreach (SecurityRequirementCondition condition in
            provider.GetConditionsForAuthorizationRequirement(requirement, context))
        {
            conditions.Add(condition);
        }

        return conditions;
    }
}

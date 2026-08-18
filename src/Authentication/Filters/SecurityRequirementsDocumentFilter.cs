using System.Collections.Generic;
using System.Linq;
using Altinn.Platform.Authentication.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Declares, per operation, the security the endpoint actually enforces, so that generated
    /// clients know a token is required and callers can see which scope each endpoint needs.
    /// </summary>
    /// <remarks>
    /// This is a document filter rather than an operation filter because a security requirement
    /// keys off a reference to a scheme in <c>components.securitySchemes</c>, and building a
    /// reference that resolves requires the document the scheme lives in.
    /// </remarks>
    public class SecurityRequirementsDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Name of the bearer-token security scheme. Must match the definition registered in
        /// <c>AddSwaggerGen</c>.
        /// </summary>
        public const string BearerSchemeId = "BearerToken";

        /// <summary>
        /// Name of the scope-carrying security scheme. Must match the definition registered in
        /// <c>AddSwaggerGen</c>.
        /// </summary>
        public const string ScopeSchemeId = "AltinnScopes";

        /// <summary>
        /// Every scope this API enforces, with the description shown in the generated document.
        /// </summary>
        /// <remarks>
        /// Machine clients obtain these from Maskinporten. <c>altinn:portal/enduser</c> is the
        /// exception - it is issued to anyone signed in to the Altinn portal.
        /// </remarks>
        public static readonly IReadOnlyDictionary<string, string> KnownScopes = new Dictionary<string, string>
        {
            [AuthzConstants.SCOPE_SYSTEMREGISTER_WRITE] = "Create and update registered systems",
            [AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN] = "Full administration of registered systems",
            [AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_READ] = "Read system user requests",
            [AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_WRITE] = "Create and manage system user requests",
            [AuthzConstants.SCOPE_SYSTEMUSER_LOOKUP] = "Look up system users",
            [AuthzConstants.SCOPE_INTERNAL_OR_PLATFORM_ACCESS] = "Internal system user administration",
            [AuthzConstants.SCOPE_CLIENTDELEGATION_READ] = "Read client delegations",
            [AuthzConstants.SCOPE_CLIENTDELEGATION_WRITE] = "Manage client delegations",
            [AuthzConstants.SCOPE_PORTAL] = "Issued to end users signed in to the Altinn portal",
        };

        /// <summary>
        /// Maps an authorization policy to the scopes it accepts. A policy maps to several scopes
        /// when it was registered with <c>RequireScopeAnyOf</c>, in which case any one of them is
        /// enough and each becomes a separate alternative in the document.
        /// </summary>
        /// <remarks>
        /// Policies that are not listed here are enforced by the policy decision point against the
        /// caller's rights rather than by a scope, so they only require a token.
        /// </remarks>
        private static readonly IReadOnlyDictionary<string, string[]> ScopesByPolicy = new Dictionary<string, string[]>
        {
            [AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE] =
                [AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN, AuthzConstants.SCOPE_SYSTEMREGISTER_WRITE],
            [AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE] = [AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_WRITE],
            [AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ] = [AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_READ],
            [AuthzConstants.POLICY_SCOPE_SYSTEMUSERLOOKUP] = [AuthzConstants.SCOPE_SYSTEMUSER_LOOKUP],
            [AuthzConstants.POLICY_SCOPE_INTERNAL_OR_PLATFORM_ACCESS] = [AuthzConstants.SCOPE_INTERNAL_OR_PLATFORM_ACCESS],
            [AuthzConstants.POLICY_CLIENTDELEGATION_READ] = [AuthzConstants.SCOPE_CLIENTDELEGATION_READ],
            [AuthzConstants.POLICY_CLIENTDELEGATION_WRITE] = [AuthzConstants.SCOPE_CLIENTDELEGATION_WRITE],
            [AuthzConstants.POLICY_SCOPE_PORTAL] = [AuthzConstants.SCOPE_PORTAL],
        };

        /// <summary>
        /// Builds the operationId for an endpoint: the controller and action name, which generators
        /// split into a client class and a method.
        /// </summary>
        /// <remarks>
        /// Also used to pair an <see cref="ApiDescription"/> with its operation in the document,
        /// so the two must stay in agreement.
        /// </remarks>
        /// <param name="apiDescription">The endpoint to name.</param>
        /// <returns>The operationId, or null for endpoints that are not controller actions.</returns>
        public static string? GetOperationId(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return null;
            }

            // Trailing "Async" is a C# convention, not part of the API surface - it should not
            // leak into the operationId, which becomes the method name in generated clients.
            string action = descriptor.ActionName;
            if (action.Length > 5 && action.EndsWith("Async", System.StringComparison.Ordinal))
            {
                action = action[..^5];
            }

            return $"{descriptor.ControllerName}_{action}";
        }

        /// <inheritdoc/>
        public void Apply(OpenApiDocument document, DocumentFilterContext context)
        {
            Dictionary<string, ApiDescription> byOperationId = [];
            foreach (ApiDescription apiDescription in context.ApiDescriptions)
            {
                if (GetOperationId(apiDescription) is { } operationId)
                {
                    byOperationId[operationId] = apiDescription;
                }
            }

            foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
            {
                foreach (KeyValuePair<System.Net.Http.HttpMethod, OpenApiOperation> entry in path.Value.Operations)
                {
                    OpenApiOperation operation = entry.Value;

                    if (operation.OperationId is null
                        || !byOperationId.TryGetValue(operation.OperationId, out ApiDescription? apiDescription))
                    {
                        continue;
                    }

                    ApplyTo(operation, apiDescription, document);
                }
            }
        }

        private static void ApplyTo(OpenApiOperation operation, ApiDescription apiDescription, OpenApiDocument document)
        {
            IList<object> metadata = apiDescription.ActionDescriptor.EndpointMetadata;

            // AllowAnonymous always wins over any Authorize attribute on the same endpoint.
            if (metadata.OfType<IAllowAnonymous>().Any())
            {
                return;
            }

            List<IAuthorizeData> authorizeData = [.. metadata.OfType<IAuthorizeData>()];
            if (authorizeData.Count == 0)
            {
                return;
            }

            // Start with a single alternative that requires only a token, then expand it once per
            // scope-backed policy. Several policies on one endpoint all apply, so the alternatives
            // are the combinations of one accepted scope from each.
            List<List<string>> alternatives = [[]];

            foreach (string policy in authorizeData
                .Select(a => a.Policy)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()!)
            {
                if (!ScopesByPolicy.TryGetValue(policy, out string[]? accepted))
                {
                    continue;
                }

                alternatives = [.. alternatives.SelectMany(a => accepted.Select(scope => new List<string>(a) { scope }))];
            }

            foreach (List<string> scopes in alternatives)
            {
                OpenApiSecurityRequirement requirement = new()
                {
                    [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = [],
                };

                if (scopes.Count > 0)
                {
                    requirement[new OpenApiSecuritySchemeReference(ScopeSchemeId, document)] = scopes;
                }

                (operation.Security ??= []).Add(requirement);
            }
        }
    }
}

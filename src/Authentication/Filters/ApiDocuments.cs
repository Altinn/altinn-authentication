using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Defines the OpenAPI documents this API publishes, what each contains, and where each one
    /// can be called.
    /// </summary>
    /// <remarks>
    /// Two audiences need different things. Vendors integrating with Altinn should see only the
    /// endpoints they can call, pointed at the environments they have access to. Developers on
    /// this service need the whole surface, pointed at local and test environments.
    /// </remarks>
    public static class ApiDocuments
    {
        /// <summary>
        /// The customer-facing document. Named "v1" because that is the document vendors already
        /// fetch, and renaming it would break their existing links.
        /// </summary>
        public const string External = "v1";

        /// <summary>
        /// The full surface, for developers working on this service.
        /// </summary>
        public const string Internal = "internal";

        /// <summary>
        /// The base path every endpoint shares. It lives in the server URLs rather than in each
        /// path, so <see cref="ApiBasePathDocumentFilter"/> strips it from the paths.
        /// </summary>
        public const string BasePath = "/authentication/api/v1";

        /// <summary>
        /// Route segment marking an endpoint as internal plumbing, regardless of its controller.
        /// </summary>
        private const string InternalRouteSegment = "/internal/";

        /// <summary>
        /// Controllers whose endpoints vendors integrate against. Everything not listed here -
        /// browser sign-in, OIDC front channel, token issuance, logout, introspection - is
        /// infrastructure that no vendor generates a client for.
        /// </summary>
        private static readonly HashSet<string> ExternalControllers = new(StringComparer.Ordinal)
        {
            // Holds the Maskinporten token exchange, which is how a vendor gets an Altinn token.
            "Authentication",
            "ChangeRequestSystemUser",
            "RequestSystemUser",
            "SystemRegister",
            "SystemUser",
            "SystemUserClientDelegation",
        };

        /// <summary>
        /// Decides whether an endpoint belongs in the named document.
        /// </summary>
        /// <param name="documentName">The document being generated.</param>
        /// <param name="apiDescription">The endpoint being considered.</param>
        /// <returns>True when the endpoint should appear in that document.</returns>
        public static bool Includes(string documentName, ApiDescription apiDescription)
        {
            if (documentName == Internal)
            {
                return true;
            }

            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor descriptor
                || !ExternalControllers.Contains(descriptor.ControllerName))
            {
                return false;
            }

            // A few endpoints on otherwise external controllers exist only for other Altinn
            // services, for example the system user stream the Register consumes.
            string route = "/" + (apiDescription.RelativePath ?? string.Empty);
            return !route.Contains(InternalRouteSegment, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The servers to advertise for the named document, already including
        /// <see cref="BasePath"/>.
        /// </summary>
        /// <param name="documentName">The document being generated.</param>
        /// <returns>The servers, most likely first.</returns>
        public static IList<OpenApiServer> ServersFor(string documentName)
        {
            (string Host, string Description)[] hosts = documentName == Internal
                ? [
                    ("https://localhost:44377", "Local development"),
                    ("https://platform.at22.altinn.cloud", "AT22"),
                  ]
                : [
                    ("https://platform.tt02.altinn.no", "Integration Test"),
                    ("https://platform.altinn.no", "Production"),
                  ];

            return [.. hosts.Select(h => new OpenApiServer { Url = h.Host + BasePath, Description = h.Description })];
        }
    }
}

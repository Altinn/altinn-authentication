using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Defines the OpenAPI documents this API publishes and what each one contains.
    /// </summary>
    /// <remarks>
    /// Two audiences need different things. Vendors integrating with Altinn should see only the
    /// endpoints they can call. Developers on this service need the whole surface. Servers and
    /// security schemes are not decided here - those come from the shared Altinn conventions in
    /// <c>Altinn.Authorization.ServiceDefaults.Swashbuckle</c>.
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
        /// The path prefix every endpoint shares. It is carried by the server URLs rather than by
        /// each path, matching how the other Altinn platform APIs present themselves.
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
        /// Builds the operationId for an endpoint: the controller and action name, which generators
        /// split into a client class and a method.
        /// </summary>
        /// <remarks>
        /// Without an operationId, generators synthesise a name from the path, which collides for
        /// route pairs that differ only by an extra segment. No Altinn package supplies this, so
        /// the convention lives here.
        /// </remarks>
        /// <param name="apiDescription">The endpoint to name.</param>
        /// <returns>The operationId, or null for endpoints that are not controller actions.</returns>
        public static string? GetOperationId(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return null;
            }

            return $"{descriptor.ControllerName}_{descriptor.ActionName}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Adds the address the service is actually listening on as the first server of the internal
    /// document, so that "Try it out" hits the running instance rather than a test environment.
    /// </summary>
    /// <remarks>
    /// The shared Altinn servers filter can do this, but it reads the first server address
    /// unconditionally and throws when there are none - which is the case under the in-memory test
    /// server, and takes the whole document down with it. This does the same job but treats a
    /// missing address as "no localhost server" instead of as an error.
    /// </remarks>
    public class LocalhostServerDocumentFilter : IDocumentFilter
    {
        private readonly IServer _server;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalhostServerDocumentFilter"/> class.
        /// </summary>
        /// <param name="server">The running server, used to discover its listening addresses.</param>
        public LocalhostServerDocumentFilter(IServer server)
        {
            _server = server;
        }

        /// <inheritdoc/>
        public void Apply(OpenApiDocument document, DocumentFilterContext context)
            => ApplyTo(document, context.DocumentName, _server.Features.Get<IServerAddressesFeature>()?.Addresses);

        /// <summary>
        /// Adds the local server to the document, given the name of the document being generated
        /// and the addresses the host reports.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Apply"/> because DocumentFilterContext cannot be constructed -
        /// its DocumentName has no setter - so this is the part a test can reach.
        /// </remarks>
        /// <param name="document">The document being generated.</param>
        /// <param name="documentName">The name of that document.</param>
        /// <param name="addresses">The addresses the host is listening on, if any.</param>
        internal static void ApplyTo(OpenApiDocument document, string documentName, IEnumerable<string>? addresses)
        {
            // Vendors have no use for a local address, so this is internal-only.
            if (documentName != ApiDocuments.Internal)
            {
                return;
            }

            if (TryCreateLocalServer(addresses) is not { } server)
            {
                return;
            }

            document.Servers ??= [];
            document.Servers.Insert(0, server);
        }

        /// <summary>
        /// Builds the local server entry for the addresses the service is listening on.
        /// </summary>
        /// <param name="addresses">The listening addresses, which may be empty or null.</param>
        /// <returns>The server entry, or null when there is no address to point at.</returns>
        internal static OpenApiServer? TryCreateLocalServer(IEnumerable<string>? addresses)
        {
            string[] candidates = [.. addresses ?? []];
            if (candidates.Length == 0)
            {
                return null;
            }

            // Prefer https when the service is listening on both.
            string address = candidates.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                ?? candidates[0];

            return new OpenApiServer
            {
                Url = address.TrimEnd('/') + ApiDocuments.BasePath,
                Description = "Local development",
            };
        }
    }
}

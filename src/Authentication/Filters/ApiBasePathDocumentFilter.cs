using System;
using System.Collections.Generic;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Removes the shared <c>/authentication/api/v1</c> prefix from every path, because the shared
    /// Altinn server URLs already carry it as their path suffix.
    /// </summary>
    /// <remarks>
    /// A server URL and a path concatenate to the real endpoint, so the prefix has to live in
    /// exactly one of them. Keeping it in the servers matches how the other Altinn platform APIs
    /// present themselves.
    /// </remarks>
    public class ApiBasePathDocumentFilter : IDocumentFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiDocument document, DocumentFilterContext context)
        {
            NormalizeServerUrls(document);

            if (document.Paths is null)
            {
                return;
            }

            string prefix = ApiDocuments.BasePath;

            List<KeyValuePair<string, IOpenApiPathItem>> entries = [.. document.Paths];
            document.Paths.Clear();

            foreach ((string path, IOpenApiPathItem item) in entries)
            {
                string trimmed = path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? path[prefix.Length..]
                    : path;

                // A path must not be empty and must start with '/'.
                if (trimmed.Length == 0)
                {
                    trimmed = "/";
                }

                document.Paths[trimmed] = item;
            }
        }

        /// <summary>
        /// Collapses the double slash the shared servers filter leaves between the host and the
        /// path suffix.
        /// </summary>
        /// <remarks>
        /// It joins the two with its own separator while <c>EnvironmentServerPathSuffix</c> is a
        /// <c>PathString</c>, which is required to start with one. The result is a literal "//"
        /// inside the server URL, which a client cannot normalize away by trimming.
        /// </remarks>
        private static void NormalizeServerUrls(OpenApiDocument document)
        {
            if (document.Servers is null)
            {
                return;
            }

            foreach (OpenApiServer server in document.Servers)
            {
                if (server.Url is null)
                {
                    continue;
                }

                int schemeEnd = server.Url.IndexOf("://", StringComparison.Ordinal);
                if (schemeEnd < 0)
                {
                    continue;
                }

                int afterScheme = schemeEnd + 3;
                server.Url = server.Url[..afterScheme] + server.Url[afterScheme..].Replace("//", "/");
            }
        }
    }
}

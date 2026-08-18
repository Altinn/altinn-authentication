using System;
using System.Collections.Generic;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Points the document at the servers for its audience, and moves the shared
    /// <c>/authentication/api/v1</c> prefix out of every path and into those server URLs.
    /// </summary>
    /// <remarks>
    /// The two halves belong together: a server URL and a path concatenate to the real endpoint,
    /// so the prefix has to live in exactly one of them. Without the strip it would appear twice
    /// in every request a generated client makes.
    /// </remarks>
    public class ApiBasePathDocumentFilter : IDocumentFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiDocument document, DocumentFilterContext context)
        {
            document.Servers = ApiDocuments.ServersFor(context.DocumentName);

            if (document.Paths is null)
            {
                return;
            }

            List<KeyValuePair<string, IOpenApiPathItem>> entries = [.. document.Paths];
            document.Paths.Clear();

            foreach ((string path, IOpenApiPathItem item) in entries)
            {
                string trimmed = path.StartsWith(ApiDocuments.BasePath, StringComparison.OrdinalIgnoreCase)
                    ? path[ApiDocuments.BasePath.Length..]
                    : path;

                // A path must not be empty and must start with '/'.
                if (trimmed.Length == 0)
                {
                    trimmed = "/";
                }

                document.Paths[trimmed] = item;
            }
        }
    }
}

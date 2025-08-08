using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Middlewares
{
    /// <summary>
    /// Middleware to inspect raw JSON in POST/PUT/PATCH requests
    /// </summary>
    public class RawJsonInspectionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawJsonInspectionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public RawJsonInspectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Processes the HTTP request and inspects the raw JSON body for invalid AccessPackage objects.
        /// Returns a 400 Bad Request if any AccessPackage object contains more than one 'urn' property.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task that represents the completion of request processing.</returns>
        public async Task InvokeAsync(HttpContext context)
        {           
            // Only inspect JSON POST/PUT/PATCH requests to the relevant endpoints
            bool isTargetEndpoint =
                context.Request.Path.StartsWithSegments("/authentication/api/v1/systemregister/vendor", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.Value.Contains("/accesspackages", StringComparison.OrdinalIgnoreCase);

            if (context.Request.ContentType?.Contains("application/json") == true &&
                context.Request.Method is "POST" or "PUT" && isTargetEndpoint)
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body, Encoding.UTF8, leaveOpen: true);

                string body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                // Inspect the raw JSON here
                if (ContainsMultipleUrnInAccessPackages(body))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/problem+json";
                    var problem = new ProblemDetails
                    {
                        Title = "Invalid AccessPackage object",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = "Each AccessPackage object must contain only one 'urn' property."
                    };
                    await context.Response.WriteAsJsonAsync(problem);
                    return;
                }
            }

            await _next(context);
        }

        /// <summary>
        /// Checks if any AccessPackage object in the JSON contains more than one 'urn' property.
        /// </summary>
        /// <param name="json">The raw JSON string to inspect.</param>
        /// <returns>True if any AccessPackage object contains more than one 'urn' property; otherwise, false.</returns>
        private bool ContainsMultipleUrnInAccessPackages(string json)
        {
            // Use a JsonDocument to parse and inspect the raw JSON
            using var doc = JsonDocument.Parse(json);

            JsonElement accessPackages;

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("accessPackages", out var accessPackagesProp) &&
                accessPackagesProp.ValueKind == JsonValueKind.Array)
            {
                accessPackages = accessPackagesProp;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                accessPackages = doc.RootElement;
            }
            else
            {
                // Not an object with accessPackages or a root array, so nothing to check
                return false;
            }

            foreach (var pkg in accessPackages.EnumerateArray())
            {
                if (pkg.ValueKind == JsonValueKind.Object)
                {
                    int urnCount = 0;
                    foreach (var prop in pkg.EnumerateObject())
                    {
                        if (prop.NameEquals("urn"))
                        {
                            urnCount++;
                        }
                    }

                    if (urnCount > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

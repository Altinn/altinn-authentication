using System;
using System.Collections.Generic;
using System.Linq;
using Altinn.Authorization.ProblemDetails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Documents the error responses an endpoint can return, so that generated clients can handle
    /// failures with a typed body instead of a bare exception.
    /// </summary>
    /// <remarks>
    /// Declared per endpoint from what it can actually produce rather than blanket-applied: an
    /// endpoint that takes no input cannot return 400, and one with no route parameter cannot
    /// return 404. Anything a controller declares explicitly with
    /// <see cref="ProducesResponseTypeAttribute"/> is left untouched.
    /// </remarks>
    public class ErrorResponsesOperationFilter : IOperationFilter
    {
        private const string JsonMediaType = "application/problem+json";

        /// <inheritdoc/>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Responses ??= [];

            IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            bool requiresAuthorization =
                !metadata.OfType<IAllowAnonymous>().Any() && metadata.OfType<IAuthorizeData>().Any();

            bool takesInput = context.ApiDescription.ParameterDescriptions.Count > 0;
            bool hasRouteParameter = context.ApiDescription.ParameterDescriptions
                .Any(p => p.Source == BindingSource.Path);

            if (takesInput)
            {
                Add(operation, context, "400", "The request is malformed or fails validation.", typeof(AltinnValidationProblemDetails));
            }

            if (requiresAuthorization)
            {
                // The default challenge writes no body, so there is nothing to describe here.
                Add(operation, context, "401", "The request has no valid token.", null);
                Add(operation, context, "403", "The token is valid but lacks the required scope or rights.", typeof(ProblemDetails));
            }

            if (hasRouteParameter)
            {
                Add(operation, context, "404", "The referenced resource does not exist.", typeof(ProblemDetails));
            }
        }

        private static void Add(
            OpenApiOperation operation,
            OperationFilterContext context,
            string statusCode,
            string description,
            Type? bodyType)
        {
            // Never override what a controller has stated for itself - it knows better.
            if (operation.Responses!.ContainsKey(statusCode))
            {
                return;
            }

            OpenApiResponse response = new() { Description = description };

            if (bodyType is not null)
            {
                response.Content = new Dictionary<string, OpenApiMediaType>
                {
                    [JsonMediaType] = new()
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(bodyType, context.SchemaRepository),
                    },
                };
            }

            operation.Responses[statusCode] = response;
        }
    }
}

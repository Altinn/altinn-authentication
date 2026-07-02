using System;
using Altinn.Authorization.ProblemDetails;

namespace Altinn.Platform.Authentication.Helpers;

/// <summary>
/// Extension helpers for working with <see cref="ValidationProblemBuilder"/>.
/// </summary>
public static class ValidationProblemBuilderExtensions
{
    // TODO: https://github.com/Altinn/altinn-authorization-utils/issues/608

    /// <summary>
    /// Merges the validation errors from several <see cref="ValidationProblemBuilder"/> instances into a single builder.
    /// </summary>
    /// <param name="builder">The builder to merge the errors into.</param>
    /// <param name="errorBuilders">The builders to merge into <paramref name="builder"/>.</param>
    public static void MergeWith(ref this ValidationProblemBuilder builder, params ReadOnlySpan<ValidationProblemBuilder> errorBuilders)
    {
        foreach (var errorBuilder in errorBuilders)
        {
            if (errorBuilder.TryBuild(out var built))
            {
                foreach (var error in built.Errors)
                {
                    builder.Add(error);
                }

                foreach (var extension in built.Extensions)
                {
                    try
                    {
                        builder.AddExtension(extension.Key, extension.Value);
                    }
                    catch (ArgumentException)
                    {
                        // Ignore duplicate extension keys
                    }
                }

                builder.Detail ??= built.Detail;
            }
        }
    }
}

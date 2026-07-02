using Altinn.Authorization.ProblemDetails;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Extension helpers for working with <see cref="ValidationProblemBuilder"/>.
    /// </summary>
    public static class ValidationProblemBuilderExtensions
    {
        /// <summary>
        /// Merges the validation errors from several <see cref="ValidationProblemBuilder"/> instances into a single builder.
        /// </summary>
        /// <param name="errorBuilders">The builders whose errors should be merged.</param>
        /// <returns>A <see cref="ValidationProblemBuilder"/> containing the errors from all supplied builders.</returns>
        public static ValidationProblemBuilder MergeValidationErrors(params ValidationProblemBuilder[] errorBuilders)
        {
            ValidationProblemBuilder mergedErrors = default;
            foreach (var errorBuilder in errorBuilders)
            {
                foreach (var error in errorBuilder)
                {
                    mergedErrors.Add(error);
                }
            }

            return mergedErrors;
        }
    }
}

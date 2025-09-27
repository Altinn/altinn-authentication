namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthorizeValidationError
    {
        public required string Error { get; init; }            // e.g. invalid_request, invalid_scope, unsupported_response_type
        public required string Description { get; init; }      // short, non-PII reason
    }
}

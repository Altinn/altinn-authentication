namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Which kind of upstream token is being validated.
    /// </summary>
    /// <remarks>
    /// Stated explicitly by the caller. It used to be inferred from whether a nonce was supplied,
    /// which is true for the id_token in the callback but not for an id_token presented as
    /// <c>id_token_hint</c> at end-session — that one arrives with no nonce, and was therefore
    /// validated as if it were an access token.
    /// </remarks>
    public enum UpstreamTokenKind
    {
        /// <summary>An id_token, issued to us as the client.</summary>
        IdToken,

        /// <summary>An access token, issued for an API rather than for us.</summary>
        AccessToken,
    }
}

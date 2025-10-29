namespace Altinn.Platform.Authentication.Persistance.Constants.OidcServer
{
    /// <summary>
    /// Constants related to the client table in the OIDC server database
    /// </summary>
    public static class ClientTable
    {
        /// <summary>
        /// Column name for client id
        /// </summary>
        public const string CLIENT_ID = "client_id";

        /// <summary>
        /// Column name for client name
        /// </summary>
        public const string CLIENT_NAME = "client_name";

        /// <summary>
        /// Column name for client type
        /// </summary>
        public const string CLIENT_TYPE = "client_type";

        /// <summary>
        /// Column name for token endpoint authentication method
        /// </summary>
        public const string TOKEN_ENDPOINT_AUTH_METHOD = "token_endpoint_auth_method";

        /// <summary>
        /// Column name for redirect uris
        /// </summary>
        public const string REDIRECT_URIS = "redirect_uris";

        /// <summary>
        /// Column name for client secret hash
        /// </summary>
        public const string CLIENT_SECRET_HASH = "client_secret_hash";

        /// <summary>
        /// Column name for client secret expiration time
        /// </summary>
        public const string CLIENT_SECRET_EXPIRES_AT = "client_secret_expires_at";

        /// <summary>
        /// Column name for when the client secret was last rotated
        /// </summary>
        public const string SECRET_ROTATION_AT = "secret_rotation_at";

        /// <summary>
        /// Column name for JSON Web Key Set (JWKS) URI
        /// </summary>
        public const string JWKS_URI = "jwks_uri";

        /// <summary>
        /// Column name for JSON Web Key Set (JWKS)
        /// </summary>
        public const string JWKS = "jwks";

        /// <summary>
        /// Column name for allowed scopes
        /// </summary>
        public const string ALLOWED_SCOPES = "allowed_scopes";

        /// <summary>
        /// Column name for when the client was created
        /// </summary>
        public const string CREATED_AT = "created_at";

        /// <summary>
        /// column name for when the client was last updated
        /// </summary>
        public const string UPDATED_AT = "updated_at";
    }
}

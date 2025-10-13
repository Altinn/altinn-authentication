namespace Altinn.Platform.Authentication.Persistance.Constants.OidcServer
{
    /// <summary>
    /// Defines constants for the "oidcserver.login_transaction_upstream" table and its columns.
    /// </summary>
    public static class UpstreamLoginTransactionTable
    {
        /// <summary>
        /// The name of the table in the database.
        /// </summary>
        public const string TABLE = "oidcserver.login_transaction_upstream";

        /// <summary>
        /// The name of the column in the database that stores the upstream request ID.
        /// </summary>
        public const string UPSTREAM_REQUEST_ID = "upstream_request_id";

        /// <summary>
        /// The name of the column in the database that stores the downstream request ID.
        /// </summary>
        public const string REQUEST_ID = "request_id";

        /// <summary>
        /// The name of the column in the database that stores the clientless request ID.
        /// </summary>
        public const string CLIENT_LESS_REQUEST_ID = "clientless_request_id";

        /// <summary>
        /// The name of the column in the database that stores the client ID.
        /// </summary>
        public const string STATUS = "status";

        /// <summary>
        /// The name of the column in the database that stores the creation timestamp.
        /// </summary>
        public const string CREATED_AT = "created_at";

        /// <summary>
        /// The name of the column in the database that stores the expiration timestamp.
        /// </summary>
        public const string EXPIRES_AT = "expires_at";

        /// <summary>
        /// The name of the column in the database that stores the completion timestamp.
        /// </summary>
        public const string COMPLETED_AT = "completed_at";

        /// <summary>
        /// The name of the column in the database that stores the provider.
        /// </summary>
        public const string PROVIDER = "provider";

        /// <summary>
        /// The name of the column in the database that stores the upstream client ID.
        /// </summary>
        public const string UPSTREAM_CLIENT_ID = "upstream_client_id";

        /// <summary>
        /// The name of the column in the database that stores the upstream redirect URI.
        /// </summary>
        public const string AUTHORIZATION_ENDPOINT = "authorization_endpoint";

        /// <summary>
        /// The name of the column in the database that stores the upstream token endpoint.
        /// </summary>
        public const string TOKEN_ENDPOINT = "token_endpoint";

        /// <summary>
        /// The name of the column in the database that stores the upstream JWKS URI.
        /// </summary>
        public const string JWKS_URI = "jwks_uri";

        /// <summary>
        /// the name of the column in the database that stores the upstream redirect URI.
        /// </summary>
        public const string UPSTREAM_REDIRECT_URI = "upstream_redirect_uri";

        /// <summary>
        /// the name of the column in the database that stores the downstream redirect URI.
        /// </summary>
        public const string STATE = "state";

        /// <summary>
        /// the name of the column in the database that stores the nonce.
        /// </summary>
        public const string NONCE = "nonce";

        /// <summary>
        /// The name of the column in the database that stores the scopes.
        /// </summary>
        public const string SCOPES = "scopes";

        /// <summary>
        /// The name of the column in the database that stores the acr_values.
        /// </summary>
        public const string ACR_VALUES = "acr_values";

        /// <summary>
        /// The name of the column in the database that stores the prompts.
        /// </summary>
        public const string PROMPTS = "prompts";

        /// <summary>
        /// The name of the column in the database that stores the ui_locales.
        /// </summary>
        public const string UI_LOCALES = "ui_locales";

        /// <summary>
        /// The name of the column in the database that stores the max_age.
        /// </summary>
        public const string MAX_AGE = "max_age";

        /// <summary>
        /// The name of the column in the database that stores the code verifier for PKCE.
        /// </summary>
        public const string CODE_VERIFIER = "code_verifier";

        /// <summary>
        /// The name of the column in the database that stores the code challenge for PKCE.
        /// </summary>
        public const string CODE_CHALLENGE = "code_challenge";

        /// <summary>
        /// The name of the column in the database that stores the code challenge method for PKCE.
        /// </summary>
        public const string CODE_CHALLENGE_METHOD = "code_challenge_method";

        /// <summary>
        /// The name of the column in the database that stores the request object JWT.
        /// </summary>
        public const string AUTH_CODE = "auth_code";

        /// <summary>
        /// The name of the column in the database that stores the timestamp when the auth code was received.
        /// </summary>
        public const string AUTH_CODE_RECEIVED_AT = "auth_code_received_at";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string ERROR = "error";

        /// <summary>
        /// The name of the column in the database that stores the error description.
        /// </summary>
        public const string ERROR_DESCRIPTION = "error_description";

        /// <summary>
        /// The name of the column in the database that stores the timestamp when the token was exchanged.
        /// </summary>
        public const string TOKEN_EXCHANGED_AT = "token_exchanged_at";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_ISSUER = "upstream_issuer";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_SUB = "upstream_sub";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_ACR = "upstream_acr";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_AUTH_TIME = "upstream_auth_time";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_ID_TOKEN_JTI = "upstream_id_token_jti";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string UPSTREAM_SESSION_SID = "upstream_session_sid";

        /// <summary>
        /// The name of the column in the database that stores the ID token.
        /// </summary>
        public const string CORRELATION_ID = "correlation_id";

        /// <summary>
        /// The name of the column in the database that stores the IP address of the client that created the transaction.
        /// </summary>
        public const string CREATED_BY_IP = "created_by_ip";

        /// <summary>
        /// The name of the column in the database that stores the user agent hash of the client that created the transaction.
        /// </summary>
        public const string USER_AGENT_HASH = "user_agent_hash";
    }
}

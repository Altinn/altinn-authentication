namespace Altinn.Platform.Authentication.Persistance.Constants.OidcServer
{
    /// <summary>
    /// Constans for the login_transaction table and its columns.
    /// </summary>
    public static class LoginTransactionTable
    {
        /// <summary>
        /// The name of the table in the database.
        /// </summary>
        public const string TABLE = "oidcserver.login_transaction";

        /// <summary>
        /// Column name for the unique identifier of the login transaction.
        /// </summary>
        public const string REQUEST_ID = "request_id";

        /// <summary>
        /// column name for the current status of the login transaction.
        /// </summary>
        public const string STATUS = "status";

        /// <summary>
        /// Column name for the timestamp when the login transaction was created.
        /// </summary>
        public const string CREATED_AT = "created_at";

        /// <summary>
        /// Column name for the timestamp when the login transaction expires.
        /// </summary>
        public const string EXPIRES_AT = "expires_at";

        /// <summary>
        /// Column name for the timestamp when the login transaction was completed. 
        /// </summary>
        public const string COMPLETED_AT = "completed_at";

        /// <summary>
        /// Column name for the subject (user) identifier once the transaction is completed.
        /// </summary>
        public const string CLIENT_ID = "client_id";

        /// <summary>
        /// Column name for the redirect URI provided by the client in the /authorize request.
        /// </summary>
        public const string REDIRECT_URI = "redirect_uri";

        /// <summary>
        /// column name for the scopes requested by the client in the /authorize request.
        /// </summary>
        public const string SCOPES = "scopes";

        /// <summary>
        /// Column name for the state parameter provided by the client in the /authorize request.
        /// </summary>
        public const string STATE = "state";

        /// <summary>
        /// Column name for the nonce parameter provided by the client in the /authorize request.
        /// </summary>
        public const string NONCE = "nonce";

        /// <summary>
        /// Column name for the acr_values parameter provided by the client in the /authorize request.
        /// </summary>
        public const string ACR_VALUES = "acr_values";

        /// <summary>
        /// Column name for the prompt parameter provided by the client in the /authorize request.
        /// </summary>
        public const string PROMPTS = "prompts";

        /// <summary>
        /// Column name for the ui_locales parameter provided by the client in the /authorize request.
        /// </summary>
        public const string UI_LOCALES = "ui_locales";

        /// <summary>
        /// Column name for the max_age parameter provided by the client in the /authorize request.
        /// </summary>
        public const string MAX_AGE = "max_age";

        /// <summary>
        /// Column name for the code_challenge parameter provided by the client in the /authorize request.
        /// </summary>
        public const string CODE_CHALLENGE = "code_challenge";

        /// <summary>
        /// Column name for the code_challenge_method parameter provided by the client in the /authorize request.
        /// </summary>
        public const string CODE_CHALLENGE_METHOD = "code_challenge_method";

        /// <summary>
        /// Column name for the request_uri parameter provided by the client in the /authorize request.
        /// </summary>
        public const string REQUEST_URI = "request_uri";

        /// <summary>
        /// Column name for the request_object_jwt parameter provided by the client in the /authorize request.
        /// </summary>
        public const string REQUEST_OBJECT_JWT = "request_object_jwt";

        /// <summary>
        /// Column name for the authorization_details parameter provided by the client in the /authorize request.
        /// </summary>
        public const string AUTHORIZATION_DETAILS = "authorization_details";

        /// <summary>
        /// Column name for the IP address of the client that initiated the login transaction.
        /// </summary>
        public const string CREATED_BY_IP = "created_by_ip";

        /// <summary>
        /// Column name for the user agent string of the client that initiated the login transaction.
        /// </summary>
        public const string USER_AGENT_HASH = "user_agent_hash";

        /// <summary>
        /// Column name for the correlation ID to trace the login transaction across systems.
        /// </summary>
        public const string CORRELATION_ID = "correlation_id";

        /// <summary>
        /// Column name for the upstream OIDC provider used for authentication.
        /// </summary>
        public const string UPSTREAM_REQUEST_ID = "upstream_request_id";
    }
}

namespace Altinn.Platform.Authentication.Persistance.Configuration
{
    /// <summary>
    /// Settings for the PostgreSQL db.
    /// </summary>
    public class PostgreSqlSettings
    {
        /// <summary>
        /// Gets or Sets the connection string to the postgres db.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or Sets the password for the app user for the postgres db.
        /// </summary>
        public string AuthenticationDbPwd { get; set; }
    }
}

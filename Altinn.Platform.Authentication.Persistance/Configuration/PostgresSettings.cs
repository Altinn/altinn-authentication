namespace Altinn.Platform.Authentication.Persistance.Configuration
{
    /// <summary>
    /// Settings for the PostgreSQL db.
    /// </summary>
    public class PostgresSettings
    {
        /// <summary>
        /// The connection string to the postgres db.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// PAssword for the app user for the postgres db.
        /// </summary>
        public string AuthorizationDbPwd { get; set; }
    }
}

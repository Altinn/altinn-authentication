using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Persistance.Configuration
{
    /// <summary>
    /// Settings for the PostgreSQL db.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PostgreSqlSettings
    {
        /// <summary>
        /// Gets or Sets the connection string to the postgres db.
        /// </summary>
        [AllowNull]
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or Sets the password for the app user for the postgres db.
        /// </summary>
        [AllowNull]
        public string AuthenticationDbPwd { get; set; }
    }
}

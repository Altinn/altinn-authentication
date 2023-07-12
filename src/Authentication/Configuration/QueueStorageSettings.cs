using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for the queue storage.
    /// </summary>
    public class QueueStorageSettings
    {
        /// <summary>
        /// ConnectionString for the storage account
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Name of the queue to push authentication event, before persisting to db.
        /// </summary>
        public string AuthenticationEventQueueName { get; set; }
    }
}

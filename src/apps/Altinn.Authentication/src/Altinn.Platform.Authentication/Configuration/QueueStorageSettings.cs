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
        public string EventLogConnectionString { get; set; }

        /// <summary>
        /// Name of the queue to push authentication event, before persisting to db.
        /// </summary>
        public string AuthenticationEventQueueName { get; set; }

        /// <summary>
        /// Number of days the queue message stored in the queue
        /// </summary>
        public int TimeToLive { get; set; } = 90;
    }
}

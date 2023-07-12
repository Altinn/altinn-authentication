using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Clients.Interfaces
{
    /// <summary>
    /// Describes the necessary methods for an implementation of an events queue client.
    /// </summary>
    public interface IEventsQueueClient
    {
        /// <summary>
        /// Enqueues the provided content to the Event Log queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> EnqueueAuthenticationEvent(string content);
    }
}

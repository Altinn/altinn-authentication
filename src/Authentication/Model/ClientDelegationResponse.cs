using System;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Represents the response containing delegation details between an agent and a client.
    /// </summary>
    public class ClientDelegationResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for the agent.
        /// </summary>
        public Guid Agent { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the client.
        /// </summary>
        public Guid Client { get; set; }
    }
}

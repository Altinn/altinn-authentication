using System;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Implementation for guid service
    /// </summary>
    public class GuidService : IGuidService
    {
        /// <inheritdoc/>
        public string NewGuid()
        {
            return Guid.NewGuid().ToString();
        }
    }
}

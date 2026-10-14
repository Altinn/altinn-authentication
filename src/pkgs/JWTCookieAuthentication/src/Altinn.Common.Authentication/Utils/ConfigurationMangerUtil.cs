using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Altinn.Common.Authentication.Utils
{
    /// <summary>
    /// Util to handles configuration managers. Singleton pattern
    /// </summary>
    public sealed class ConfigurationMangerUtil
    {
        private static readonly Lazy<ConfigurationMangerUtil> Lazy =
            new Lazy<ConfigurationMangerUtil>(() => new ConfigurationMangerUtil());

        /// <summary>
        /// Access to the single instance
        /// </summary>
        public static ConfigurationMangerUtil Instance => Lazy.Value;

        private ConfigurationMangerUtil()
        {
            ConfigManagers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();
        }

        /// <summary>
        /// The config managers for the different well know urls
        /// </summary>
        public ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers { get; set; }
    }
}

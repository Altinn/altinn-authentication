using System.Collections.Generic;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Authentication.Extensions
{
    /// <summary>
    /// Extension for ServiceCollectionExtension
    /// </summary>
    public static class ServiceCollectionExtension
    {
        /// <summary>
        /// Configure the OIDC providers
        /// </summary>
        public static IServiceCollection ConfigureOidcProviders(
            this IServiceCollection services,
            string sectionName)
        {
            services.AddOptions<OidcProviderSettings>()
                .Configure((OidcProviderSettings settings, IConfiguration configuration) =>
                {
                    var section = configuration.GetSection(sectionName);
                    IEnumerable<IConfigurationSection> providerSections = section.GetChildren();

                    foreach (IConfigurationSection providerSection in providerSections)
                    {
                        OidcProvider prov = new OidcProvider();
                        providerSection.Bind(prov);
                        prov.IssuerKey = providerSection.Key;
                        settings.Add(prov.IssuerKey, prov);
                    }
                });

            return services;
        }
    }
}

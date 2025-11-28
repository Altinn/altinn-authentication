#nullable enable

using System;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.Authentication.Configuration;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Altinn.Platform.Authentication.Tests;

public class WebApplicationFixture
    : IAsyncLifetime
{
    private readonly WebApplicationFactory _factory = new();

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    public WebApplicationFactory<Program> CreateServer(Action<IServiceCollection>? configureServices = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            if (configureServices is not null)
            {
                builder.ConfigureTestServices(configureServices);
            }
        });
    }

    private class WebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(config =>
            {
                var testConfig = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();
                config.AddConfiguration(testConfig);
            });

            builder.ConfigureTestServices(services =>
            {
                var timeProvider = new AdvanceableTimeProvider();

                // services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                
                // services.AddSingleton<IPostConfigureOptions<OidcProviderSettings>, OidcProviderPostConfigureSettingsStub>();
                services.AddSingleton<TimeProvider>(timeProvider);

                // services.AddSingleton<AdvanceableTimeProvider>(timeProvider);

                services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            });

            base.ConfigureWebHost(builder);
        }
    }
}

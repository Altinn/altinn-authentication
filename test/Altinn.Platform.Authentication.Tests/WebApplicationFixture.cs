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
using Microsoft.Extensions.Time.Testing;
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

    public WebApplicationFactory<Program> CreateServer(
        Action<IWebHostBuilder>? configureBuilder = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            if (configureBuilder is not null)
            {
                configureBuilder(builder);
            }

            if (configureServices is not null)
            {
                builder.ConfigureServices(configureServices);
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
                    .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true)
                    .Build();
                config.AddConfiguration(testConfig);
            });

            builder.ConfigureTestServices(services =>
            {
                var timeProvider = new FakeTimeProvider();

                // services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();

                // services.AddSingleton<IPostConfigureOptions<OidcProviderSettings>, OidcProviderPostConfigureSettingsStub>();
                services.AddSingleton(timeProvider);
                services.AddSingleton<TimeProvider>(timeProvider);

                // services.AddSingleton<AdvanceableTimeProvider>(timeProvider);

                services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            });

            base.ConfigureWebHost(builder);
        }
    }
}

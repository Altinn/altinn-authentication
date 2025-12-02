using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Clients;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Common.PEP.Authorization;
using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Implementation;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Authorization;
using Altinn.Platform.Authentication.Core.Clients.Interfaces;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Core.Services.Validation;
using Altinn.Platform.Authentication.Core.Telemetry;
using Altinn.Platform.Authentication.Extensions;
using Altinn.Platform.Authentication.Filters;
using Altinn.Platform.Authentication.Health;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.JwtCookie;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Altinn.Platform.Authentication;

/// <summary>
/// Configures the Authentication Host and Azure Application Insight Telemetry
/// </summary>
internal static class AuthenticationHost
{
    /// <summary>
    /// Creates the Authentication app from config
    /// </summary>
    /// <param name="args">input arguments</param>
    /// <returns></returns>
    internal static WebApplication Create(string[] args)
    {
        var builder = AltinnHost.CreateWebApplicationBuilder("authentication", args, opts => opts.ConfigureEnabledServices(services => services.DisableApplicationInsights()));
        var services = builder.Services;
        var config = builder.Configuration;
        var descriptor = services.GetAltinnServiceDescriptor();
        
        MapPostgreSqlConfiguration(builder, descriptor);

        if (!builder.Environment.IsDevelopment())
        {
            if (builder.Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey") is var key && !string.IsNullOrEmpty(key))
            {
                builder.Services.AddOpenTelemetry()
                    .UseAzureMonitor(m =>
                    {
                        m.ConnectionString = string.Format("InstrumentationKey={0}", key);
                    });
            }
        }

        services.AddAutoMapper(cfg => { }, typeof(Program));
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = true;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddMvc().AddControllersAsServices();
        services.AddHealthChecks().AddCheck<HealthCheck>("authentication_health_check");

        services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
        services.Configure<PaginationOptions>(config.GetSection("PaginationOptions"));
        services.Configure<Altinn.Common.PEP.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));
        services.Configure<Altinn.Authentication.Integration.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));
        services.Configure<AccessManagementSettings>(config.GetSection("AccessManagementSettings"));
        services.Configure<Altinn.Platform.Authentication.Model.KeyVaultSettings>(config.GetSection("kvSetting"));
        services.Configure<CertificateSettings>(config.GetSection("CertificateSettings"));
        services.Configure<QueueStorageSettings>(config.GetSection("QueueStorageSettings"));
        services.Configure<Altinn.Common.AccessToken.Configuration.KeyVaultSettings>(config.GetSection("kvSetting"));

        services.Configure<AccessTokenSettings>(config.GetSection("AccessTokenSettings"));

        // Configure OIDC providers used by JwtCookieHandler
        services.Configure<Altinn.Common.Authentication.Configuration.OidcProviderSettings>(config.GetSection("OidcProviders"));

        // Configure OIDC providers used by authentication
        services.ConfigureOidcProviders("OidcProviders");

        services.ConfigureDataProtection(builder.Environment.IsDevelopment(), config.GetSection("AzureStorageConfiguration").Get<AzureStorageConfiguration>());
        services.AddAuthentication(JwtCookieDefaults.AuthenticationScheme)
             .AddJwtCookie(JwtCookieDefaults.AuthenticationScheme, options =>
             {
                 GeneralSettings generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();
                 options.JwtCookieName = generalSettings.JwtCookieName;
                 options.MetadataAddress = generalSettings.OpenIdWellKnownEndpoint;
                 options.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuerSigningKey = true,
                     ValidateIssuer = false,
                     ValidateAudience = false,
                     RequireExpirationTime = true,
                     ValidateLifetime = true,
                     ClockSkew = TimeSpan.FromSeconds(10)
                 };

                 if (builder.Environment.IsDevelopment())
                 {
                     options.RequireHttpsMetadata = false;
                 }
             });

        services.AddHttpClient<ISblCookieDecryptionService, SblCookieDecryptionService>();
        services.AddHttpClient<IUserProfileService, UserProfileService>();
        services.AddHttpClient<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationService>();
        services.AddHttpClient<IOrganisationsService, OrganisationsService>();
        services.AddHttpClient<AuthorizationApiClient>();
        services.AddHttpClient<IAccessManagementClient, AccessManagementClient>();
        services.AddHttpClient<IResourceRegistryClient, ResourceRegistryClient>();
        services.AddHttpClient<IPartiesClient, PartiesClient>();
        services.AddHttpClient<IProfile, ProfileService>();
        services.AddHttpClient<IOidcDownstreamLogout, OidcDownstreamLogoutClient>();
        services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
        services.AddTransient<ISigningCredentialsResolver, SigningCredentialsResolver>();
        services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProvider>();
        services.AddSingleton<ISigningKeysRetriever, SigningKeysRetriever>();
        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProvider>();
        services.AddSingleton<IAccessTokenValidator, AccessTokenValidator>();
        services.AddSingleton<IEFormidlingAccessValidator, EFormidlingAccessValidator>();
        services.AddHttpClient<IOidcProvider, OidcProviderService>();
        services.AddSingleton<IAuthentication, AuthenticationCore>();
        services.AddSingleton<IEventsQueueClient, EventsQueueClient>();
        services.AddSingleton<IEventLog, EventLogService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISystemUserService, SystemUserService>();
        services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
        services.AddSingleton<IRequestSystemUser, RequestSystemUserService>();
        services.AddSingleton<IChangeRequestSystemUser, ChangeRequestSystemUserService>();
        services.AddSingleton<IGuidService, GuidService>();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IPDP, PDPAppSI>();
        services.AddSingleton<IAuthorizationHandler, ScopeAccessHandler>();
        services.AddTransient<IAuthorizationHandler, AccessTokenHandler>();
        services.AddTransient<IAuthorizationHandler, ResourceAccessHandler>();
        services.AddTransient<DelegationHelper, DelegationHelper>();
        services.AddScoped<IOidcServerService, OidcServerService>();
        services.AddSingleton<IAuthorizeRequestValidator, AuthorizeRequestValidator>();
        services.AddSingleton<IAuthorizeClientPolicyValidator, AuthorizeClientPolicyValidator>();
        services.AddSingleton<IUpstreamTokenValidator, UpstreamTokenValidator>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<ITokenIssuer, TokenIssuerService>();
        services.TryAddSingleton<AuthenticationTelemetry>();

        services.ConfigureOpenTelemetryTracerProvider(m => m.AddSource(AuthenticationTelemetry.Name));
        services.AddOpenTelemetry()
            .AddAssemblyMetrics<Program>() // Authentication
            .AddAssemblyMetrics<AuthenticationTelemetry>() // Authentication.Core
            .AddAssemblyMetrics<SystemUserRepository>() // Authentication.Persistance
            .AddAssemblyMetrics<PartiesClient>(); // Authentication.Integration

        services.AddAntiforgery(options =>
        {
            // asp .net core expects two types of tokens: One that is attached to the request as header, and the other one as cookie.
            // The values of the tokens are not the same and both need to be present and valid in a "unsafe" request.

            // We use this for OIDC state validation. See authentication controller. 
            // https://docs.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-3.1
            // https://github.com/axios/axios/blob/master/lib/defaults.js
            options.Cookie.Name = "AS-XSRF-TOKEN";
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.HeaderName = "X-XSRF-TOKEN";
        });

        // Add Swagger support (Swashbuckle)
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Altinn Platform Authentication", Version = "v1" });

            try
            {
                string filePath = GetXmlCommentsPathForControllers();
                c.IncludeXmlComments(filePath);
            }
            catch
            {
                // catch swashbuckle exception if it doesn't find the generated xml documentation file
            }
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN, AuthzConstants.SCOPE_SYSTEMREGISTER_WRITE))
            .AddPolicy(AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ, policy =>
                policy.Requirements.Add(new ResourceAccessRequirement("read", "altinn_access_management")))
            .AddPolicy(AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE, policy =>
                policy.Requirements.Add(new ResourceAccessRequirement("write", "altinn_access_management")))
            .AddPolicy(AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_WRITE))
            .AddPolicy(AuthzConstants.POLICY_SCOPE_SYSTEMUSERLOOKUP, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_SYSTEMUSER_LOOKUP))
            .AddPolicy(AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_SYSTEMUSER_REQUEST_READ))
            .AddPolicy(AuthzConstants.POLICY_SCOPE_INTERNAL_OR_PLATFORM_ACCESS, policy =>
                    policy.Requirements.Add(new InternalScopeOrAccessTokenRequirement(
                        AuthzConstants.SCOPE_INTERNAL_OR_PLATFORM_ACCESS)))
            .AddPolicy(AuthzConstants.POLICY_CLIENTDELEGATION_READ, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_CLIENTDELEGATION_READ))
            .AddPolicy(AuthzConstants.POLICY_CLIENTDELEGATION_WRITE, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_CLIENTDELEGATION_WRITE))
            .AddPolicy(AuthzConstants.POLICY_SCOPE_PORTAL, policy =>
                policy.RequireScopeAnyOf(AuthzConstants.SCOPE_PORTAL));

        services.AddFeatureManagement();
        services.AddScoped<TrimStringsActionFilter>();

        builder.AddPersistanceLayer();

        return builder.Build();
    }

    private static string GetXmlCommentsPathForControllers()
    {
        // locate the xml file being generated by .NET
        string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

        return xmlPath;
    }

    // Note: eventually we can rename the configuration values and remove this mapping
    private static void MapPostgreSqlConfiguration(IHostApplicationBuilder builder, AltinnServiceDescriptor serviceDescriptor)
    {
        var runMigrations = builder.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection");
        var adminConnectionStringFmt = builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthenticationDbAdminConnectionString");
        var adminConnectionStringPwd = builder.Configuration.GetValue<string>("PostgreSQLSettings:authenticationDbAdminPwd", defaultValue: string.Empty);
        var connectionStringFmt = builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthenticationDbUserConnectionString");
        var connectionStringPwd = builder.Configuration.GetValue<string>("PostgreSQLSettings:authenticationDbPwd", defaultValue: string.Empty);

        if (adminConnectionStringFmt is not null && connectionStringFmt is not null)
        {
            var adminConnectionString = string.Format(adminConnectionStringFmt, adminConnectionStringPwd);
            var connectionString = string.Format(connectionStringFmt, connectionStringPwd);

            var existing = builder.Configuration.GetValue<string>($"ConnectionStrings:{serviceDescriptor.Name}_db");
            if (!string.IsNullOrEmpty(existing))
            {
                return;
            }

            builder.Configuration.AddInMemoryCollection([
                KeyValuePair.Create($"ConnectionStrings:{serviceDescriptor.Name}_db", connectionString),
                KeyValuePair.Create($"ConnectionStrings:{serviceDescriptor.Name}_db_migrate", adminConnectionString),
                KeyValuePair.Create($"Altinn:Npgsql:{serviceDescriptor.Name}:Migrate:Enabled", runMigrations ? "true" : "false"),
            ]);
        }
    }
}

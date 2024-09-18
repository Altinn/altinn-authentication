using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Clients;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Common.PEP.Authorization;
using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Configuration;
using Altinn.Common.PEP.Implementation;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Extensions;
using Altinn.Platform.Authentication.Filters;
using Altinn.Platform.Authentication.Health;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.Configuration;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Telemetry;
using AltinnCore.Authentication.JwtCookie;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Yuniql.AspNetCore;
using Yuniql.Extensibility;
using Yuniql.PostgreSql;

ILogger logger;

string applicationInsightsKeySecretName = "ApplicationInsights--InstrumentationKey";
string postgresConfigKeySecretNameAdmin = "PostgreSQLSettings--AuthenticationDbAdminConnectionString";
string postgresConfigKeySecretNameUser = "PostgreSQLSettings--AuthenticationDbUserConnectionString";

string applicationInsightsConnectionString = string.Empty;
string postgresAdminConnectionString = string.Empty;
string postgresUserConnectionString = string.Empty;

var builder = WebApplication.CreateBuilder(args);

ConfigureSetupLogging();

await SetConfigurationProviders(builder.Configuration);

ConfigureLogging(builder.Logging);

ConfigureServices(builder.Services, builder.Configuration);

// Forwardlimit is set to 2 as our infrastructure has 1 proxy forward. The 2nd value from right to left is read into remoteipaddress property which is the client ip
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.RequireHeaderSymmetry = false;
});

builder.Services.AddPersistanceLayer();

var app = builder.Build();

ConfigurePostgreSql();

Configure();

app.Run();

void ConfigureSetupLogging()
{
    var logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning)
            .AddFilter("Altinn.Platform.Authentication.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;

    config.SetBasePath(basePath);
    config.AddJsonFile(basePath + @"altinn-appsettings/altinn-dbsettings-secret.json", true, true);

    if (basePath == "/")
    {
        // In a pod/container where the app is located in an app folder on the root of the filesystem.
        string filePath = basePath + @"app/appsettings.json";
        config.AddJsonFile(filePath, false, true);
    }
    else
    {
        // Running on development machine.
        string filePath = Directory.GetCurrentDirectory() + @"/appsettings.json";
        config.AddJsonFile(filePath, false, true);
    }

    config.AddEnvironmentVariables();

    if (!builder.Environment.IsDevelopment())
    {
        await ConnectToKeyVaultAndSetApplicationInsights(config);
        await ConnectToKeyVaultAndSetConfig(config);
    }

    config.AddCommandLine(args);
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    logger.LogInformation("Program // Connect to key vault and set up application insights");

    Altinn.Common.AccessToken.Configuration.KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);

    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", keyVaultSettings.ClientId);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", keyVaultSettings.ClientSecret);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", keyVaultSettings.TenantId);

        try
        {
            SecretClient client = new SecretClient(new Uri(keyVaultSettings.SecretUri), new EnvironmentCredential());
            KeyVaultSecret secret = await client.GetSecretAsync(applicationInsightsKeySecretName);
            applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", secret.Value);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, $"Unable to read application insights key.");
        }

        try
        {
            config.AddAzureKeyVault(
                 keyVaultSettings.SecretUri, 
                 keyVaultSettings.ClientId, 
                 keyVaultSettings.ClientSecret);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, $"Unable to add key vault secrets to config.");
        }
    }
}

async Task ConnectToKeyVaultAndSetConfig(ConfigurationManager config)
{
    logger.LogInformation("Program // Connect to key vault and set up configuration");

    Altinn.Common.AccessToken.Configuration.KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);

    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", keyVaultSettings.ClientId);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", keyVaultSettings.ClientSecret);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", keyVaultSettings.TenantId);

        await SetUpAzureInsights(keyVaultSettings, config);        
        AddAzureKeyVault(keyVaultSettings, config);

        await SetUpPostgresConfigFromKeyVault(keyVaultSettings, config);
    }
}

async Task SetUpAzureInsights(Altinn.Common.AccessToken.Configuration.KeyVaultSettings keyVaultSettings, ConfigurationManager config)
{
    try
    {
        SecretClient client = new SecretClient(new Uri(keyVaultSettings.SecretUri), new EnvironmentCredential());
        KeyVaultSecret secret = await client.GetSecretAsync(applicationInsightsKeySecretName);
        applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", secret.Value);
    }
    catch (Exception vaultException)
    {
        logger.LogError(vaultException, $"Unable to read application insights key.");
    }
}

async Task SetUpPostgresConfigFromKeyVault(Altinn.Common.AccessToken.Configuration.KeyVaultSettings keyVaultSettings, ConfigurationManager config)
{
    try
    {
        SecretClient client = new(new Uri(keyVaultSettings.SecretUri), new EnvironmentCredential());
        KeyVaultSecret secretAdmin = await client.GetSecretAsync(postgresConfigKeySecretNameAdmin);
        postgresAdminConnectionString = secretAdmin.Value;

        KeyVaultSecret secretUser = await client.GetSecretAsync(postgresConfigKeySecretNameUser);
        postgresUserConnectionString = secretUser.Value;

    }
    catch (Exception postgresConfigException) 
    {
        logger.LogError(postgresConfigException, "Program // Unable to read postgres config from key vault.");
    }
}

void AddAzureKeyVault(Altinn.Common.AccessToken.Configuration.KeyVaultSettings keyVaultSettings, ConfigurationManager config)
{
    try
    {
        config.AddAzureKeyVault(
             keyVaultSettings.SecretUri, keyVaultSettings.ClientId, keyVaultSettings.ClientSecret);
    }
    catch (Exception vaultException)
    {
        logger.LogError(vaultException, $"Unable to add key vault secrets to config.");
    }
}

void ConfigureLogging(ILoggingBuilder logging)
{
    // The default ASP.NET Core project templates call CreateDefaultBuilder, which adds the following logging providers:
    // Console, Debug, EventSource
    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1

    // Clear log providers
    logging.ClearProviders();

    // Setup up application insight if ApplicationInsightsConnectionString is available
    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        // Add application insights https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger
        logging.AddApplicationInsights(
         configureTelemetryConfiguration: (config) => config.ConnectionString = applicationInsightsConnectionString,
         configureApplicationInsightsLoggerOptions: (options) => { });

        // Optional: Apply filters to control what logs are sent to Application Insights.
        // The following configures LogLevel Information or above to be sent to
        // Application Insights for all categories.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Warning);

        // Adding the filter below to ensure logs of all severity from Program.cs
        // is sent to ApplicationInsights.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Trace);
    }
    else
    {
        // If not application insight is available log to console
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddConsole();
    }
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    services.AddMvc().AddControllersAsServices();
    services.AddHealthChecks().AddCheck<HealthCheck>("authentication_health_check");

    services.AddSingleton(config);
    services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));     
    services.Configure<Altinn.Common.PEP.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));
    services.Configure<Altinn.Authentication.Integration.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));
    services.Configure<AccessManagementSettings>(config.GetSection("AccessManagementSettings"));
    services.Configure<Altinn.Platform.Authentication.Model.KeyVaultSettings>(config.GetSection("kvSetting"));
    services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));
    services.Configure<CertificateSettings>(config.GetSection("CertificateSettings"));
    services.Configure<QueueStorageSettings>(config.GetSection("QueueStorageSettings"));
    services.Configure<Altinn.Common.AccessToken.Configuration.KeyVaultSettings>(config.GetSection("kvSetting"));

    services.Configure<AccessTokenSettings>(config.GetSection("AccessTokenSettings"));
    services.ConfigureOidcProviders(config.GetSection("OidcProviders"));
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

    services.AddSingleton(config); 
    services.AddHttpClient<ISblCookieDecryptionService, SblCookieDecryptionService>();
    services.AddHttpClient<IUserProfileService, UserProfileService>();
    services.AddHttpClient<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationService>();
    services.AddHttpClient<IOrganisationsService, OrganisationsService>();
    services.AddHttpClient<AuthorizationApiClient>();
    services.AddHttpClient<IAccessManagementClient, AccessManagementClient>();
    services.AddHttpClient<IPartiesClient, PartiesClient>();
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
    services.AddSingleton<IGuidService, GuidService>();
    services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddSingleton<IPDP, PDPAppSI>();
    services.AddSingleton<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddTransient<IAuthorizationHandler, ResourceAccessHandler>();

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel() { StorageFolder = "/tmp/logtelemetry" });
        services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
        {
            ConnectionString = applicationInsightsConnectionString
        });

        services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();
        services.AddApplicationInsightsTelemetryProcessor<IdentityTelemetryFilter>();
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
    }

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
            policy.RequireScopeAnyOf(AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN))
        .AddPolicy(AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ, policy => 
            policy.Requirements.Add(new ResourceAccessRequirement("read", "altinn_access_management")))
        .AddPolicy(AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE, policy => 
            policy.Requirements.Add(new ResourceAccessRequirement("write", "altinn_access_management"))); 

    services.AddFeatureManagement();
}

static string GetXmlCommentsPathForControllers()
{
    // locate the xml file being generated by .NET
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    return xmlPath;
}

void Configure()
{
    app.UseForwardedHeaders();
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseDeveloperExceptionPage();

        // Enable higher level of detail in exceptions related to JWT validation
        IdentityModelEventSource.ShowPII = true;
    }
    else
    {
        app.UseExceptionHandler("/authentication/api/v1/error");
    }

    app.UseSwagger(o => o.RouteTemplate = "authentication/swagger/{documentName}/swagger.json");

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/authentication/swagger/v1/swagger.json", "Altinn Platform Authentication API");
        c.RoutePrefix = "authentication/swagger";
    });

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
}

void ConfigurePostgreSql() 
{
    if (builder.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
    {
        ConsoleTraceService traceService = new() { IsDebugEnabled = true };

        string workspacePath = Path.Combine(Environment.CurrentDirectory, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));
        if (builder.Environment.IsDevelopment())
        {
            string connectionString = string.Format(
            builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthenticationDbAdminConnectionString"));
            workspacePath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).FullName, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));
            postgresAdminConnectionString = connectionString;
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(postgresAdminConnectionString);

        var user = connectionStringBuilder.Username;

        app.UseYuniql(
            new PostgreSqlDataService(traceService),
            new PostgreSqlBulkImportService(traceService),
            traceService,
            new Configuration
            {
                Environment = "prod",
                Workspace = workspacePath,
                ConnectionString = postgresAdminConnectionString,
                IsAutoCreateDatabase = false,
                IsDebug = true,
                Tokens = [KeyValuePair.Create("YUNIQL-USER", user)]
            });
    }
}

/// <summary>
/// Startup class.
/// </summary>
public partial class Program
{
}

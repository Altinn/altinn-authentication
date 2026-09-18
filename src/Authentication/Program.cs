using System;
using System.Linq;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Platform.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

WebApplication app = AuthenticationHost.Create(args);

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

// For debugging purposes, will be removed before production.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.Use(next => context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var config = context.RequestServices.GetRequiredService<IOptionsMonitor<AltinnClusterInfo>>();
        logger.LogWarning(
            "Request {method} {scheme}://{host}{path} from {ip}. Trusted proxies: {TrustedProxies}. Headers: {headers}",
            context.Request.Method,
            context.Request.Scheme,
            context.Request.Host,
            context.Request.Path,
            context.Connection?.RemoteIpAddress,
            config.CurrentValue.TrustedProxies,
            string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}: {h.Value}")));
        return next(context);
    });
}

app.UseForwardedHeaders();

// For debugging purposes, will be removed before production.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.Use(next => context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(
            "Request {method} {scheme}://{host}{path} from {ip}.",
            context.Request.Method,
            context.Request.Scheme,
            context.Request.Host,
            context.Request.Path,
            context.Connection?.RemoteIpAddress);
        return next(context);
    });
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

await app.RunAsync();

/// <summary>
/// Startup class.
/// </summary>
public partial class Program
{
}

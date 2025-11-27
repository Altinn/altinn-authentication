using System;
using System.Collections.Generic;
using System.IO;
using Altinn.Platform.Authentication;
using Altinn.Platform.Authentication.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Npgsql;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

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

app.UseForwardedHeaders();

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

if (app.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
{
    ConsoleTraceService traceService = new() { IsDebugEnabled = true };

    var connectionString = app.Configuration.GetValue<string>("PostgreSQLSettings:AuthenticationDbAdminConnectionString");
    string workspacePath = Path.Combine(Environment.CurrentDirectory, app.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));

    if (app.Environment.IsDevelopment())
    {
        workspacePath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).FullName, app.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));
    }

    var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
    var userConStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);

    var user = connectionStringBuilder.Username;
    var appUser = userConStringBuilder.Username;

    app.UseYuniql(
        new PostgreSqlDataService(traceService),
        new PostgreSqlBulkImportService(traceService),
        traceService,
        new Configuration
        {
            Environment = "prod",
            Workspace = workspacePath,
            ConnectionString = connectionString,
            IsAutoCreateDatabase = false,
            IsDebug = true,
            Tokens = [
                KeyValuePair.Create("YUNIQL-USER", user),
                KeyValuePair.Create("APP-USER", user)
            ]
        });
}

await app.RunAsync();

/// <summary>
/// Startup class.
/// </summary>
public partial class Program
{
}

using Altinn.Platform.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
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

await app.RunAsync();

/// <summary>
/// Startup class.
/// </summary>
public partial class Program
{
}

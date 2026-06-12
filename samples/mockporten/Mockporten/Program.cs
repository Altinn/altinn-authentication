using Altinn.Authorization.ServiceDefaults;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Mockporten.Configuration;
using Mockporten.Services;
using Mockporten.Services.Implementation;
using Mockporten.Services.Interface;
using Mockporten.Services.Interfaces;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = AltinnHost.CreateWebApplicationBuilder("mockporten", args);
var services = builder.Services;

// Load secrets from the mockporten Key Vault as a configuration source. The
// vault URI is supplied per environment via kvSetting:KeyVaultURI (already
// configured in Azure). A secret named "GeneralSettings--TestIdpSharedPassword"
// binds to GeneralSettings:TestIdpSharedPassword (the Key Vault config provider
// maps "--" to ":"). See #1983.
var keyVaultUri = builder.Configuration["kvSetting:KeyVaultURI"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

services.AddOptions<GeneralSettings>()
    .BindConfiguration("GeneralSettings")
    .ValidateOnStart();

services.AddOptions<KeyVaultSettings>()
    .BindConfiguration("kvSetting")
    .ValidateOnStart();

services.AddControllersWithViews();

services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProvider>();
services.AddSingleton<IToken, TokenService>();
services.AddSingleton<ISharedAccessPasswordValidator, SharedAccessPasswordValidator>();

services.AddLogging();
services.AddApplicationInsightsTelemetry(opts =>
{
    opts.ConnectionString = builder.Configuration["APPINSIGHTS_CONNECTIONSTRING"];
});

var app = builder.Build();
var env = app.Environment;

if (env.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();

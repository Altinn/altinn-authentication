using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

public class BFFClient
{
    public EnvironmentHelper EnvironmentHelper { get; set; }
    public readonly string? BaseUrl;

    public BFFClient()
    {
        EnvironmentHelper = PlatformAuthenticationClient.LoadEnvironment();
        BaseUrl = GetEnvironment(EnvironmentHelper.Testenvironment);
    }

    public static string GetEnvironment(string environmentHelperTestenvironment)
    {
        // Define base URLs for tt02 and all "at" environments
        const string tt02 = "https://platform.tt02.altinn.no/";
        const string atBaseUrl = "https://platform.{env}.altinn.cloud/";

        // Handle case-insensitive input and return the correct URL
        environmentHelperTestenvironment = environmentHelperTestenvironment.ToLower();

        return environmentHelperTestenvironment switch
        {
            "tt02" => tt02,
            "at22" or "at23" or "at24" => atBaseUrl.Replace("{env}", environmentHelperTestenvironment),
            _ => throw new ArgumentException($"Unknown environment: {environmentHelperTestenvironment}")
        };
    }

}
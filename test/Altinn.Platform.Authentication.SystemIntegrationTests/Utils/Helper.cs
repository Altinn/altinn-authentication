using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

/// <summary>
/// helper class for file utils and test data generation purposes etc
/// </summary>
public class Helper
{
    /// <summary>
    /// Read a test file for test data generation mostly used for creating request bodies
    /// </summary>
    /// <param name="path">Path to file. For instance: "Resources/Testdata/Systemregister/CreateNewSystem.json"</param>
    /// <returns></returns>
    public static async Task<string> ReadFile(string? path)
    {
        var basePath = Directory.GetCurrentDirectory();
        Assert.True(path != null, "Path cannot be null");
        var filePath = Path.Combine(basePath, path);
        var stringBody = await File.ReadAllTextAsync(filePath);
        return stringBody;
    }
}
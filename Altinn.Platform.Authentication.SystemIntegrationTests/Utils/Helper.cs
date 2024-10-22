using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.AccessManagement.SystemIntegrationTests.Utils;

/// <summary>
/// helper class for file utils and test data generation purposes etc
/// </summary>
public class Helper
{
    /// <summary>
    /// For loggin
    /// </summary>
    public ITestOutputHelper Output { get; }

    /// <summary>
    /// Helper class that loads environment and creates platform client
    /// </summary>
    /// <param name="output">for logging</param>
    /// <exception cref="Exception">Throws exception if unable to set environment</exception>
    public Helper(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Loads test environment and other settings
    /// </summary>
    /// <param name="path">File path for environment file</param>
    /// <returns></returns>
    public static EnvironmentHelper LoadEnvironment(string path)
    {
        var basePath = Directory.GetCurrentDirectory();
        var filePath = Path.Combine(basePath, path);
        var environmentFile = File.ReadAllText(filePath);
        var env = JsonSerializer.Deserialize<EnvironmentHelper>(environmentFile);
        Assert.True(env != null, $"Environment file {filePath} not found");
        return env;
    }

    /// <summary>
    /// For test data generation purposes
    /// </summary>
    /// <param name="length">Length of string</param>
    /// <returns></returns>
    public static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }


    /// <summary>
    /// Read a test file for test data generation mostly used for creating request bodies
    /// </summary>
    /// <param name="path">Path to file. For instance: "Resources/Testdata/Systemregister/CreateNewSystem.json"</param>
    /// <returns></returns>
    public static async Task<string> ReadFile(string path)
    {
        var basePath = Directory.GetCurrentDirectory();
        var filePath = Path.Combine(basePath, path);
        var stringBody = await File.ReadAllTextAsync(filePath);
        return stringBody;
    }
}
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

/// <summary>
/// helper class for file utils and test data generation purposes etc
/// </summary>
public class Helper
{
    /// <summary>
    /// For loggin
    /// </summary>
    

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
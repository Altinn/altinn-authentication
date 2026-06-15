using System.Text.Json;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Models;

public class AccessReasonTests
{
    [Fact]
    public void TestJsonDeserialization_1()
    {
        // example from AM
        var json = """
            {
                "flag": 1,
                "items": [
                    {
                    "name": "direct",
                    "description": "Access granted directly with assignment"
                    }
                ]
            }
            """;

        var reason = JsonSerializer.Deserialize<AccessReason>(json);
        Assert.NotNull(reason);
        Assert.True(reason.ToEnum() == AccessReasonFlag.Direct);
    }

    [Fact]
    public void TestJsonDeserialization_MissMatch()
    {
        var json = """
            {
                "flag": 3,
                "items": [
                    {
                    "name": "direct",
                    "description": "Access granted directly with assignment"
                    }
                ]
            }
            """;

        // we only care about the flag
        var reason = JsonSerializer.Deserialize<AccessReason>(json);
        Assert.NotNull(reason);
        Assert.True(reason.ToEnum() == (AccessReasonFlag.Direct | AccessReasonFlag.ClientDelegation));
        Assert.Equal(2, reason.Items.Count);
    }

    [Fact]
    public void TestJsonDeserialization_Null()
    {
        var json = "null";

        var reason = JsonSerializer.Deserialize<AccessReason>(json);
        Assert.Null(reason);
    }

    [Fact]
    public void TestJsonSerialization_Empty()
    {
        var json = "{}";

        var reason = JsonSerializer.Deserialize<AccessReason>(json);
        Assert.NotNull(reason);
        Assert.True(reason.ToEnum() == AccessReasonFlag.None);
        Assert.Empty(reason.Items);
    }
}
namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

using System.Collections.Generic;

public class TestUsersConfig
{
    public Dictionary<string, EnvironmentUsers> Environments { get; set; }
}

public class EnvironmentUsers
{
    public List<TestUser> DAGL { get; set; } = new();
}

public class TestUser
{
    public string AltinnPartyId { get; set; }
    public string Pid { get; set; }
    public string UserId { get; set; }
}
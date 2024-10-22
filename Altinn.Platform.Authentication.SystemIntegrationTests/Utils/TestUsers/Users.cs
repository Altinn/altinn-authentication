using Altinn.AccessManagement.SystemIntegrationTests.Domain;

namespace Altinn.AccessManagement.SystemIntegrationTests.Utils.TestUsers;

public class Users
{
    /// <summary>
    /// Returns a test user with manager privileges
    /// </summary>
    /// <returns></returns>
    public static AltinnUser GetManager()
    {
        const string userId = "20012785";
        const string partyId = "51135683";
        const string pid = "02875698977";

        return new AltinnUser
            { userId = userId, partyId = partyId, pid = pid };
    }
}
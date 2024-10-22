namespace Altinn.AccessManagement.SystemIntegrationTests.Utils;

/// <summary>
/// Keeping track of state of the test object during tests. Mainly useful when linking more than one api requests together 
/// </summary>
public class Teststate
{
    public string systemId;

    /// <summary>
    /// Organization number for vendors used when creating new systems for a vendor: authentication/api/v1/systemregister/vendor
    /// </summary>
    public string vendorId;
}
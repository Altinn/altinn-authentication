namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

public class SystemRegisterState
{
    public string VendorId { get; set; }
    public string Name { get; set; }
    public string ClientId { get; set; }
    public string SystemId => $"{VendorId}_{Name}"; // Combination of vendorId and randomName
    
    public List<Right> Rights { get; set; }


    public SystemRegisterState(string vendorId, string randomName)
    {
        VendorId = vendorId;
        Name = randomName;
        ClientId = Guid.NewGuid().ToString();
        
        Rights =
        [
            new Right
            {
                Resource =
                [
                    new Resource
                    {
                        Value = "kravogbetaling",
                        Id = "urn:altinn:resource"
                    }
                ]
            }
        ];
    }
}
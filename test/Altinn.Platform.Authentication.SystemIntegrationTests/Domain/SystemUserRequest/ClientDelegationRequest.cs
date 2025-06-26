using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemUserRequest;

public class ClientDelegationRequest
{
    public string SystemId { get; set; } = default!;
    public string? PartyOrgNo { get; set; } = default!;
    public string ExternalRef { get; set; } = default!;
    public string RedirectUrl { get; set; } = default!;
    public List<SystemRegisterAccessPackageDto>? AccessPackages { get; set; } = [];
}

public class AccessPackageDto
{
    public string Urn { get; set; } = default!;
}


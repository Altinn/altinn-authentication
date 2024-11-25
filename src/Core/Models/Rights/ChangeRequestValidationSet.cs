using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

public class ChangeRequestValidationSet
{
    public ExternalRequestId ExternalRequestId { get; set; }

    public RegisteredSystem RegisteredSystem { get; set; }

    public SystemUser SystemUser { get; set; }
}

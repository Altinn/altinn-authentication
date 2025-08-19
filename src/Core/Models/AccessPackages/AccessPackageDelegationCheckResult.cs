using Altinn.Platform.Authentication.Core.Models.Rights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    public record AccessPackageDelegationCheckResult(bool CanDelegate, List<AccessPackage>? accessPackages);
}

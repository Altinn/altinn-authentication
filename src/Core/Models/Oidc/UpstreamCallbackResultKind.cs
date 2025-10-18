using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public enum UpstreamCallbackResultKind
    {
        RedirectToClient,          // success: code+state to downstream client
        RedirectToGoTo,
        ErrorRedirectToClient,     // OIDC error back to downstream client
        LocalError                 // 4xx/5xx here
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public class AuthenticateFromSessionInput
    {
        public string SessionHandle { get; set; }
    }
}

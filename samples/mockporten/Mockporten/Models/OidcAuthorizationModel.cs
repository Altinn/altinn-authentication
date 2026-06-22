using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockporten.Models
{
    public class OidcAuthorizationModel
    {
        public string UserClaims { get; set; }

        public string Redirect_uri { get; set; }

        public string Scope { get; set; }

        public string Response_type { get; set; }

        public string Client_id { get; set; }

        public string State { get; set; }

        public string Nonce { get; set; }
        public string Acr_values { get; set; }
        public string Response_mode { get; set; }
        public string Ui_locales { get; set; }
        public string Prompt { get; set; }
        public string Code_challenge { get; set; }
        public string Code_challenge_method { get; set; }
        public string Login_hint { get; set; }
        public string Claims { get; set; }
        public string Request_uri { get; set; }

        public string Pid { get; set;}

        /// <summary>
        /// The single shared access password granting the right to use the
        /// Test-IDP. Posted in the form body only - never a query parameter.
        /// </summary>
        public string Password { get; set; }
    }
}

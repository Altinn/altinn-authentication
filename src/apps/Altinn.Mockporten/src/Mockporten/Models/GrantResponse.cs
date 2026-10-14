using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockporten.Models
{
    public class GrantResponse
    {
        public string client_id { get; set; }

        public string id_token { get; set; }

        public string access_token { get; set; } = string.Empty;

        public string token_type { get; set; } = "Bearer";

        public int expires_in { get; set; } = 3600;

        public string refresh_token { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class TestTokenUtil
    {
        private const string OrganisationIdentity = "OrganisationLogin";

        public static string GetTestToken()
        {
            // Arrange
            List<Claim> claims = [];

            string orgNr = "974760223";

            object iso6523Consumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{orgNr}"
            };

            claims.Add(new Claim("consumer", JsonSerializer.Serialize(iso6523Consumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:instances.write altinn:instances.read"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));
            claims.Add(new Claim("jti", "fe155387-c5f2-42e9-943a-811789db663a"));

            ClaimsIdentity identity = new(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new(identity);

            return JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));
        }
    }
}

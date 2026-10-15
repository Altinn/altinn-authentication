using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class ProfileMock : IProfile
    {
        public Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup)
        {
            // Lookup by SSN is used by the ID-porten token exchange (Profile/Register lookup). Return a
            // fully populated profile (matching the canonical test user) so the default (Profile) path
            // issues a complete token.
            if (!string.IsNullOrEmpty(profileLookup.Ssn))
            {
                UserProfile bySsn = new UserProfile
                {
                    UserId = 20000,
                    PartyId = 50001,
                    UserName = "steph",
                    UserUuid = Guid.NewGuid(),
                    Party = new Party() { PartyId = 50001, SSN = profileLookup.Ssn, Person = new Person() { SSN = profileLookup.Ssn } }
                };
                return Task.FromResult(bySsn);
            }

            UserProfile userProfile = new UserProfile
            {
                UserId = profileLookup.UserId,
                UserUuid = Guid.NewGuid(),
                Party = new Party() { PartyId = profileLookup.UserId, SSN = "12345678901", Person = new Person() { SSN = "12345678901" } }
            };
            return Task.FromResult(userProfile);
        }
    }
}
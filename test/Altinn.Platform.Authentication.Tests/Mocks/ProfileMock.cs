using Altinn.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using Altinn.Platform.Register.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class ProfileMock : IProfile
    {
        public Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup)
        {
            UserProfile userProfile = new UserProfile();
            List<UserProfile> profileList = new List<UserProfile>();
            string profilesPath = GetUserProfilePath();
            if (File.Exists(profilesPath))
            {
                string content = File.ReadAllText(profilesPath);
                profileList = JsonSerializer.Deserialize<List<UserProfile>>(content);
            }

            if (profileLookup.Ssn != null)
            {
                userProfile = profileList.Find(p => p.Party.SSN == profileLookup.Ssn);
                if (userProfile == null)
                {
                    userProfile.Party = new Party() { SSN = profileLookup.Ssn };    
                    userProfile.UserId = profileLookup.UserId;
                    userProfile.UserUuid = Guid.NewGuid();
                }

                return Task.FromResult(userProfile);
            }

            return Task.FromResult(userProfile);
        }

        private static string GetUserProfilePath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "Data", "UserProfile", "UserProfiles.json");
        }
    }
}

using Altinn.Platform.Authentication.Core.Models;

namespace Altinn.Platform.Authentication.Mappers
{
    /// <summary>
    /// AutoMapper profile for Authentication
    /// </summary>
    public class AuthenticationMapper : AutoMapper.Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationMapper"/> class.
        /// </summary>
        public AuthenticationMapper() 
        {
            AllowNullCollections = true;
            CreateMap<SystemUserInternalDTO, SystemUserExternalDTO>();
            CreateMap<SystemUserDetailInternalDTO, SystemUserExternalDTO>();
            CreateMap<SystemUserInternalDTO, SystemUserDetailExternalDTO>();
            CreateMap<SystemUserInternalDTO, SystemUserDetailInternalDTO>();
            CreateMap<SystemUserDetailInternalDTO, SystemUserDetailExternalDTO>();
        }
    }
}

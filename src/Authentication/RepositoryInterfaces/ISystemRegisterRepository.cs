using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.RepositoryInterfaces;

/// <summary>
/// Interface to the System Register Repository
/// </summary>
public interface ISystemRegisterRepository
{
    /// <summary>
    /// Returns the list of currently available (is_deleted = false ) Registered Systems
    /// </summary>
    /// <returns>List of SystemRegister</returns>
    Task<List<RegisteredSystem>> GetAllSystems();
}

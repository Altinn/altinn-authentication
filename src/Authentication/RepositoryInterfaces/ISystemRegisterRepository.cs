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
    Task<List<RegisteredSystem>> GetAllActiveSystems();

    /// <summary>
    /// Inserts a new Registered System, using an optimistic choice for the ID
    /// if the ID already exists, an integer is appended to it's end.
    /// </summary>
    /// <returns></returns>
    Task<string> CreateRegisteredSystem(RegisteredSystem toBeInserted);

    /// <summary>
    /// Returns a single RegisteredSystem, even if it was set to deleted.
    /// </summary>
    /// <returns></returns>
    Task<RegisteredSystem> GetRegisteredSystemById(string id);
}

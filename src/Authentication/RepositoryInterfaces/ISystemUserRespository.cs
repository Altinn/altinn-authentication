using System.Threading.Tasks;

using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.RepositoryInterfaces
{
    /// <summary>
    /// Repository Interface, to be implemented in the Persistance layer
    /// </summary>
    public interface ISystemUserRespository
    {
        /// <summary>
        /// Insert the descriptor of the new SystemUser, returns the persisted SystemUser with a db generated Entity ID
        /// </summary>
        /// <param name="toBeInserted">The desciptor of the new SystemUser to be inserted, has either a null ID, or an ID provided by the frontend, the db generate a UUID if none is provided.</param>
        /// <returns></returns>
        Task<SystemUser> InsertSystemUser(SystemUser toBeInserted); 
    }
}
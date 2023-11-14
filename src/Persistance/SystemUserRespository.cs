using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.Configuration;
using Altinn.Platform.Authentication.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance
{
    /// <summary>
    /// SystemUser Repository.
    /// </summary>
    public class SystemUserRespository : ISystemUserRespository
    {
        private readonly string _connectionString;

        private readonly string insertSystemIntegration =
            "SELECT * FROM altinn_authentication.insert_system_user_integration(@_integration_title, @_integration_description, @_product_name, @_owned_by_party_id, @_supplier_name, @_supplier_org_no, @_client_id)";

        private class Column
        {
            internal const string Id = "system_user_integration_id";
            internal const string IntegrationTitle = "_integration_title";
            internal const string Description = "_integration_description";
            internal const string ProductName = "_product_name";
            internal const string OwnedByPartyId = "_owned_by_party_id";
            internal const string SupplierName = "_supplier_name";
            internal const string SupplierOrgNo = "_supplier_org_no";
            internal const string ClientId = "_client_id";
            internal const string IsDeleted = "_is_deleted";
            internal const string Created = "_created";
        }

        /// <summary>
        /// SystemUserRepository Constructor
        /// </summary>
        /// <param name="postgresSettings"> Holds the Connection string to PostgresSQL db, and pwd to Authorization db</param>
        public SystemUserRespository(IOptions<PostgresSQLSettings> postgresSettings)
        {
            _connectionString = string.Format(postgresSettings.Value.ConnectionString, postgresSettings.Value.AuthorizationDbPwd);
        }

        /// <inheritdoc />
        public async Task<SystemUser> InsertSystemUser(SystemUser toBeInserted)
        {
            return await InsertSystemUserDb(toBeInserted);
        }

        private async Task<SystemUser> InsertSystemUserDb(SystemUser toBeInserted)
        {
            try
            {
                await using NpgsqlConnection connection = new(_connectionString);
                await connection.OpenAsync();

                NpgsqlCommand command = new(insertSystemIntegration, connection);
                command.Parameters.AddWithValue(Column.IntegrationTitle, toBeInserted.IntegrationTitle);
                command.Parameters.AddWithValue(Column.Description, toBeInserted.Description);
                command.Parameters.AddWithValue(Column.ProductName, toBeInserted.ProductName);
                command.Parameters.AddWithValue(Column.OwnedByPartyId, toBeInserted.OwnedByPartyId);
                command.Parameters.AddWithValue(Column.SupplierName, toBeInserted.SupplierName);
                command.Parameters.AddWithValue(Column.SupplierOrgNo, toBeInserted.SupplierOrgNo);
                command.Parameters.AddWithValue(Column.ClientId, toBeInserted.ClientId);
                command.Parameters.AddWithValue(Column.IsDeleted, toBeInserted.IsDeleted);

                return toBeInserted;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}

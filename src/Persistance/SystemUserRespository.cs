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
            "SELECT * FROM altinn_authentication.insert_system_user_integration(" +
            "@integration_title, " +
            "@integration_description, " +
            "@product_name, " +
            "@owned_by_party_id, " +
            "@supplier_name, " +
            "@supplier_org_no, " +
            "@client_id)";

        /// <summary>
        /// Private helper class to hold the Column names as constant strings to aid in typing SQL commands.
        /// </summary>
        private class Column
        {
            internal const string Id = "system_user_integration_id";       // UUID : Normally set by the db using gen 4 uuid random generator by default, but could also be set by the Frontend. 
            internal const string IntegrationTitle = "integration_title";  // User's chosen name for this Integration
            internal const string Description = "integration_description"; // User's own written description of this Integration
            internal const string ProductName = "product_name";            // The chosen "Of the shelf Product". The self made systems are not implemented yet
            internal const string OwnedByPartyId = "owned_by_party_id";    // The user that owns this Integration
            internal const string SupplierName = "supplier_name";          // Of the shelf product vendor
            internal const string SupplierOrgNo = "supplier_org_no";       // Of the shelf product vendor
            internal const string ClientId = "client_id";                  // Not implemented yet. Will be used instead of SupplierName and OrgNo for Persons
            internal const string IsDeleted = "is_deleted";                // Used instead of regular deletion
            internal const string Created = "created";                     // Always set by the db
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

                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.Read())
                {
                    return ConvertFromReaderToSystemUser(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static SystemUser ConvertFromReaderToSystemUser(NpgsqlDataReader reader)
        {
            return new SystemUser
            {
                Id = reader.GetFieldValue<string>(Column.Id),
                Description = reader.GetFieldValue<string>(Column.Description),
                ProductName = reader.GetFieldValue<string>(Column.ProductName),
                OwnedByPartyId = reader.GetFieldValue<string>(Column.OwnedByPartyId),
                SupplierName = reader.GetFieldValue<string>(Column.SupplierName),
                SupplierOrgNo = reader.GetFieldValue<string>(Column.SupplierOrgNo),
                ClientId = reader.GetFieldValue<string>(Column.ClientId),
                IntegrationTitle = reader.GetFieldValue<string>(Column.IntegrationTitle),
                Created = reader.GetFieldValue<DateTime>(Column.Created)
            };
        }
    }
}

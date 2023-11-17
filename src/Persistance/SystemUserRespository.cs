using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    [ExcludeFromCodeCoverage]
    public class SystemUserRespository : ISystemUserRespository
    {
        private readonly string _connectionString;

        private readonly string insertSystemIntegration =
            "SELECT * FROM altinn_authentication.insert_system_user_integration(" +
            "@_integration_title, " +
            "@_integration_description, " +
            "@_product_name, " +
            "@_owned_by_party_id, " +
            "@_supplier_name, " +
            "@_supplier_org_no, " +
            "@_client_id)";

        /// <summary>
        /// Private helper class to hold the Column names of the System_User_Integration table as constant strings to aid in typing SQL commands.
        /// Prefix with an underscore when using them as input Parameters to the Functions: see the In_() method.         
        /// </summary>
        private static class Params
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
        public SystemUserRespository(IOptions<PostgreSqlSettings> postgresSettings)
        {
            _connectionString = string.Format(postgresSettings.Value.ConnectionString, postgresSettings.Value.AuthenticationDbPwd);
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

                command.Parameters.AddWithValue(In_(Params.IntegrationTitle), toBeInserted.IntegrationTitle);
                command.Parameters.AddWithValue(In_(Params.Description), toBeInserted.Description);
                command.Parameters.AddWithValue(In_(Params.ProductName), toBeInserted.ProductName);
                command.Parameters.AddWithValue(In_(Params.OwnedByPartyId), toBeInserted.OwnedByPartyId);
                command.Parameters.AddWithValue(In_(Params.SupplierName), toBeInserted.SupplierName);
                command.Parameters.AddWithValue(In_(Params.SupplierOrgNo), toBeInserted.SupplierOrgNo);
                command.Parameters.AddWithValue(In_(Params.ClientId), toBeInserted.ClientId);
                command.Parameters.AddWithValue(In_(Params.IsDeleted), toBeInserted.IsDeleted);

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
                Id = reader.GetFieldValue<string>(Params.Id),
                Description = reader.GetFieldValue<string>(Params.Description),
                ProductName = reader.GetFieldValue<string>(Params.ProductName),
                OwnedByPartyId = reader.GetFieldValue<string>(Params.OwnedByPartyId),
                SupplierName = reader.GetFieldValue<string>(Params.SupplierName),
                SupplierOrgNo = reader.GetFieldValue<string>(Params.SupplierOrgNo),
                ClientId = reader.GetFieldValue<string>(Params.ClientId),
                IntegrationTitle = reader.GetFieldValue<string>(Params.IntegrationTitle),
                Created = reader.GetFieldValue<DateTime>(Params.Created)
            };
        }

        private static string In_(string field)
        {
            return "_" + field;
        }
    }
}

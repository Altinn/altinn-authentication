﻿using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.RepositoryInterfaces;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance
{
    /// <summary>
    /// SystemUser Repository.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class SystemUserRespository : ISystemUserRespository
    {
        private readonly NpgsqlDataSource _dataSource;        

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
        /// <param name="dataSource">Holds the Postgres db datasource</param>
        public SystemUserRespository(
            NpgsqlDataSource dataSource)      
        {
            //_connectionString = string.Format(postgresSettings.Value.ConnectionString, postgresSettings.Value.AuthenticationDbPwd);
            _dataSource = dataSource;
        }

        /// <inheritdoc />
        public async Task<bool> SetDeleteSystemUserById(Guid id)
        {
            const string QUERY = /*strpsql*/@"
                UPDATE altinn_authentication.system_user_integration
	            SET is_deleted = TRUE
        	    WHERE altinn_authentication.system_user_integration.system_user_integration_id = @system_user_integration_id;
	            GET DIAGNOSTICS success = ROW_COUNT;
	            RETURN success > 0;
                ";

            try
            {
                await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

                command.Parameters.AddWithValue(Params.Id);

                return await command.ExecuteEnumerableAsync()
                    .SelectAwait(ConvertFromReaderToBoolean)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<SystemUser>> GetAllActiveSystemUsersForParty(int partyId)
        {
            const string QUERY = /*strpsql*/@"
                SELECT 
                    system_user_integration_id,
		            integration_title,
		            integration_description,
		            product_name,
		            owned_by_party_id,
		            supplier_name,
		            supplier_org_no,
		            client_id,
		            is_deleted,
		            created                    
                FROM altinn_authentication.system_user_integration sui 
	            WHERE sui.owned_by_party_id = @owned_by_party_id	
	                  AND sui.is_deleted = false;
                ";

            try
            {
                await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

                command.Parameters.AddWithValue(Params.OwnedByPartyId, partyId);

                IAsyncEnumerable<NpgsqlDataReader> list = command.ExecuteEnumerableAsync();
                return await list.SelectAwait(ConvertFromReaderToSystemUser).ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SystemUser> GetSystemUserById(Guid id)
        {
            const string QUERY = /*strpsql*/@"
            SELECT 
	    	    system_user_integration_id,
		        integration_title,
		        integration_description,
		        product_name,
		        owned_by_party_id,
		        supplier_name,
		        supplier_org_no,
		        client_id,
		        is_deleted,
		        created
	        FROM altinn_authentication.system_user_integration sui 
	        WHERE sui.system_user_integration_id = @system_user_integration_id
	            AND sui.is_deleted = false;
            ";

            try
            {
                await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);
                command.Parameters.AddWithValue(Params.Id, id);

                return await command.ExecuteEnumerableAsync()
                    .SelectAwait(ConvertFromReaderToSystemUser)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SystemUser> InsertSystemUser(SystemUser toBeInserted)
        {
            const string QUERY = /*strpsql*/@"            
                INSERT INTO altinn_authentication.system_user_integration(
                    integration_title,
                    integration_description,
                    product_name,
                    owned_by_party_id,
                    supplier_name,
                    supplier_org_no,
                    client_id)
                VALUES(
                    @integration_title,
                    @integration_description,
                    @product_name,
                    @owned_by_party_id,
                    @supplier_name,
                    @supplier_org_no,
                    @client_id    
                )
                RETURNING system_user_integration_id;";

            try
            {
                await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);
                
                command.Parameters.AddWithValue(Params.IntegrationTitle, toBeInserted.IntegrationTitle);
                command.Parameters.AddWithValue(Params.Description, toBeInserted.Description);
                command.Parameters.AddWithValue(Params.ProductName, toBeInserted.ProductName);
                command.Parameters.AddWithValue(Params.OwnedByPartyId, toBeInserted.OwnedByPartyId);
                command.Parameters.AddWithValue(Params.SupplierName, toBeInserted.SupplierName);
                command.Parameters.AddWithValue(Params.SupplierOrgNo, toBeInserted.SupplierOrgNo);
                command.Parameters.AddWithValue(Params.ClientId, toBeInserted.ClientId);
                command.Parameters.AddWithValue(Params.IsDeleted, toBeInserted.IsDeleted);

                return await command.ExecuteEnumerableAsync()
                    .SelectAwait(ConvertFromReaderToSystemUser)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static ValueTask<SystemUser> ConvertFromReaderToSystemUser(NpgsqlDataReader reader)
        {
            return new ValueTask<SystemUser>(new SystemUser 
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
            });
        }

        private static ValueTask<bool> ConvertFromReaderToBoolean(NpgsqlDataReader reader)
        {
            return new ValueTask<bool>(reader.GetBoolean(0));
        }
    }
}

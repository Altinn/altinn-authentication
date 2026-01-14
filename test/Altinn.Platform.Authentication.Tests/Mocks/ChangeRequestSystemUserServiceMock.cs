using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ChangeRequestSystemUserServiceMock : IChangeRequestSystemUser
    {
        /// <summary>
        /// The Constructor
        /// </summary>
        public ChangeRequestSystemUserServiceMock()
        {    
        }

        public Task<Result<bool>> ApproveAndDelegateChangeOnSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponseInternal>> CheckUserAuthorizationAndGetRequest(Guid requestId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> CreateChangeRequest(ChangeRequestSystemUser createRequest, OrganisationNumber vendorOrgNo, SystemUserInternalDTO systemUser)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> CreateChangeRequest(ChangeRequestSystemUser createRequest, OrganisationNumber vendorOrgNo, SystemUserInternalDTO systemUser, Guid correllationId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> DeleteChangeRequestByRequestId(Guid requestId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<Page<ChangeRequestResponse, Guid>>> GetAllChangeRequestsForVendor(OrganisationNumber vendorOrgNo, string systemId, Page<Guid>.Request continueRequest, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> GetChangeRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> GetChangeRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> GetChangeRequestByPartyAndRequestId(int party, Guid requestId)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<string>> GetRedirectByChangeRequestId(Guid requestId)
        {
            return await Task.FromResult("https://smartcloudaltinn.azurewebsites.net/changerequest");
        }

        public Task<Result<bool>> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> VerifySetOfRights(ChangeRequestSystemUser validateSet, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ChangeRequestResponse>> VerifySetOfRights(ChangeRequestResponse validateSet, SystemUserInternalDTO systemUser, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }
    }
}
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
    public class RequestSystemUserServiceMock : IRequestSystemUser
    {
        /// <summary>
        /// The Constructor
        /// </summary>
        public RequestSystemUserServiceMock()
        {    
        }

        public Task<Result<bool>> ApproveAndCreateClientSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> ApproveAndCreateSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<RequestSystemResponse>> CreateRequest(CreateRequestSystemUser createRequest, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> DeleteRequestByRequestId(Guid requestId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<Page<RequestSystemResponse, Guid>>> GetAllRequestsForVendor(OrganisationNumber vendorOrgNo, string systemId, Page<Guid>.Request continueRequest, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ClientRequestSystemResponse>> GetClientRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<string>> GetRedirectByRequestId(Guid requestId)
        {
            return await Task.FromResult("https://smartcloudaltinn.azurewebsites.net/request");
        }

        public Task<Result<RequestSystemResponse>> GetRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<RequestSystemResponse>> GetRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
        {
            throw new NotImplementedException();
        }

        public Task<Result<RequestSystemResponse>> GetRequestByPartyAndRequestId(int party, Guid requestId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> RejectClientSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> RejectSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
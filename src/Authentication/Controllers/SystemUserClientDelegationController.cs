using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for handling system user client delegation
    /// </summary>
    [Route("authentication/api/v1/enduser/systemuser")]
    [ApiController]
    public class SystemUserClientDelegationController(
        SystemUserController inner,
        IPDP Pdp,
        ISystemUserService SystemUserService,
        IPartiesClient PartiesClient) : ControllerBase
    {
        private const string ClientDelegationResource = "altinn_client_administration";

        /// <summary>
        /// Get Clients who can delegate to the system user
        /// </summary>
        [Authorize(Policy = AuthzConstants.POLICY_CLIENTDELEGATION_READ)]
        [HttpGet("{systemUserId}/clients/available")]
        public async Task<ActionResult<SystemUserInfo>> GetAvailableClientsForDelegation([FromRoute] Guid systemUserId)
        {            
            SystemUser systemUser = await SystemUserService.GetSingleSystemUserById(systemUserId);

            Party party = await PartiesClient.GetPartyByOrgNo(systemUser.ReporteeOrgNo);

            if (!party.PartyUuid.HasValue)
            {
                return Unauthorized();
            }

            Guid facilitatorId = party.PartyUuid.Value;
            List<string> packages = [.. systemUser?.AccessPackages.Select(P => P?.Urn != null ? P.Urn.Split(':').Last() : null).Where(p => !string.IsNullOrEmpty(p))];

            var result = await inner.GetClientsForFacilitator(facilitatorId, packages);

            // If the result is a problem (not 200 OK), return it directly
            if (result.Result is ObjectResult objectResult && objectResult.StatusCode != 200)
            {
                return result.Result;
            }

            // Otherwise, get the value
            var customerList = result.Result as OkObjectResult;
            List<Customer>? customers = customerList.Value as List<Customer>;
            if (customers == null)
            {
                return NotFound();
            }

            return await MapCustomerToSystemUserInfo(customers, systemUserId, systemUser.ReporteeOrgNo);
        }

        /// <summary>
        /// Get the list of clients that have been delegated to the specified system user
        /// </summary>
        /// <param name="systemUserId">the system user id</param>
        /// <returns></returns>
        [Authorize(Policy = AuthzConstants.POLICY_CLIENTDELEGATION_READ)]
        [HttpGet("{systemUserId}/clients")]
        public async Task<ActionResult<SystemUserInfo>> GetClientsDelegatedToSystemUser([FromRoute] Guid systemUserId)
        {
            SystemUser systemUser = await SystemUserService.GetSingleSystemUserById(systemUserId);

            Party party = await PartiesClient.GetPartyByOrgNo(systemUser.ReporteeOrgNo);
            if (!party.PartyUuid.HasValue)
            {
                return Unauthorized();
            }

            bool isAuthorized = await AuthorizeResourceAccess(ClientDelegationResource, party.PartyUuid.Value, User, "read");
            if (!isAuthorized)
            {
                return Forbid();
            }

            var result = await SystemUserService.GetListOfDelegationsForAgentSystemUser(party.PartyId, party.PartyUuid.Value, systemUserId);
            
            // If the result is a problem (not 200 OK), return it directly
            if (result.IsProblem)
            {
                return result.Problem.ToActionResult();
            }

            // Otherwise, get the value
            List<DelegationResponse>? delegationResponses = result.Value;
            if (delegationResponses == null)
            {
                return NotFound();
            }

            return await MapDelegationResponseToSystemUserInfo(delegationResponses, systemUserId, systemUser);
        }

        /// <summary>
        /// Delegate a client to the system user
        /// </summary>
        /// <param name="systemUserId">the unique identifier of the system user</param>
        /// <param name="clientId">the id of the customer</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>delegation response</returns>
        [HttpPost("{systemUserId}/clients/{clientId}")]
        public async Task<ActionResult<List<DelegationResponse>>> DelegateClientToSystemUser([FromRoute] Guid systemUserId, [FromRoute] string clientId, CancellationToken cancellationToken)
        {
            SystemUser systemUser = await SystemUserService.GetSingleSystemUserById(systemUserId);
            Party party = await PartiesClient.GetPartyByOrgNo(systemUser.ReporteeOrgNo);
            if (!party.PartyUuid.HasValue)
            {
                return Unauthorized();
            }

            bool isAuthorized = await AuthorizeResourceAccess(ClientDelegationResource, party.PartyUuid.Value, User, "write");
            if (!isAuthorized)
            {
                return Forbid();
            }

            AgentDelegationInputDto agentDelegationInput = new AgentDelegationInputDto
            {
                CustomerId = clientId,
                FacilitatorId = party.PartyUuid.Value.ToString(),                
            };

            return await inner.DelegateToAgentSystemUser(party.PartyId.ToString(), systemUserId, agentDelegationInput, cancellationToken);
        }

        /// <summary>
        /// Delegate a client to the system user
        /// </summary>
        /// <param name="systemUserId">the unique identifier of the system user</param>
        /// <param name="clientId">the id of the customer</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>delegation response</returns>
        [HttpDelete("{systemUserId}/clients/{clientId}")]
        public async Task<ActionResult<List<DelegationResponse>>> RemoveClientFromSystemUser([FromRoute] Guid systemUserId, [FromRoute] string clientId, CancellationToken cancellationToken)
        {
            Guid delegationId = Guid.Empty;
            SystemUser systemUser = await SystemUserService.GetSingleSystemUserById(systemUserId);
            Party party = await PartiesClient.GetPartyByOrgNo(systemUser.ReporteeOrgNo);
            if (!party.PartyUuid.HasValue)
            {
                return Unauthorized();
            }

            bool isAuthorized = await AuthorizeResourceAccess(ClientDelegationResource, party.PartyUuid.Value, User, "write");
            if (!isAuthorized)
            {
                return Forbid();
            }

            var result = await SystemUserService.GetListOfDelegationsForAgentSystemUser(party.PartyId, party.PartyUuid.Value, systemUserId);
            if (result.IsSuccess)
            {
                DelegationResponse? delegation = result.Value?.FirstOrDefault(d => d.CustomerId == new Guid(clientId));
                if (delegation == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Delegation not found",
                        Detail = $"No delegation found for customer {clientId} and system user {systemUserId}",
                        Status = 404
                    });
                }
                else
                {
                    delegationId = delegation.DelegationId;
                }
            }
            else if (result.IsProblem)
            {
                return result.Problem.ToActionResult();
            }

            AgentDelegationInputDto agentDelegationInput = new AgentDelegationInputDto
            {
                CustomerId = clientId,
                FacilitatorId = party.PartyUuid.Value.ToString(),
            };

            return await inner.DeleteCustomerFromAgentSystemUser(party.PartyId.ToString(), delegationId, systemUserId, cancellationToken);
        }

        private async Task<SystemUserInfo> MapCustomerToSystemUserInfo(List<Customer> customers, Guid systemUserId, string systemUserOwner)
        {
            SystemUserInfo systemUser = new SystemUserInfo
            {
                SystemUserId = systemUserId,
                SystemUserOwnerOrganizationNumber = systemUserOwner,
                Clients = new List<ClientInfo>()
            };

            foreach (var customer in customers)
            {
                systemUser.Clients.Add(new ClientInfo
                {
                    ClientOrganizationNumber = customer.OrganizationIdentifier,
                    ClientOrganizationName = customer.DisplayName,
                    AccessPackages = customer.Access
                                    .SelectMany(a => a.Packages)
                                    .Where(p => !string.IsNullOrWhiteSpace(p))
                                    .Select(p => new AccessPackage { Urn = $"urn:altinn:accesspackage:{p}" })
                                    .ToList(),
                });                     
            }

            return systemUser;
        }

        private async Task<SystemUserInfo> MapDelegationResponseToSystemUserInfo(List<DelegationResponse> delegationResponses, Guid systemUserId, SystemUser systemUser)
        {
            SystemUserInfo systemUserInfo = new SystemUserInfo
            {
                SystemUserId = systemUserId,
                SystemUserOwnerOrganizationNumber = systemUser.ReporteeOrgNo,
                Clients = new List<ClientInfo>()
            };

            foreach (var delegationResponse in delegationResponses)
            {
                Party party = await PartiesClient.GetPartyByUuId(delegationResponse.CustomerId ?? Guid.Empty);
                systemUserInfo.Clients.Add(new ClientInfo
                {
                    ClientOrganizationNumber = party?.Organization?.OrgNumber,
                    ClientOrganizationName = delegationResponse?.CustomerName,
                    AccessPackages = systemUser?.AccessPackages
                });
            }

            return systemUserInfo;
        }

        /// <summary>
        /// Determines whether the specified user is authorized to perform a given action on a resource.
        /// </summary>
        /// <remarks>This method evaluates access control by creating a decision request based on the
        /// provided resource, resource party, user, and action. It communicates with a policy decision point (PDP) to
        /// retrieve an authorization decision and validates the response.</remarks>
        /// <param name="resource">The identifier of the resource to be accessed.</param>
        /// <param name="resourceParty">The unique identifier of the resource owner or party associated with the resource.</param>
        /// <param name="userPrincipal">The <see cref="ClaimsPrincipal"/> representing the user whose access is being evaluated. Cannot be <see
        /// langword="null"/>.</param>
        /// <param name="action">The action the user intends to perform on the resource (e.g., "read", "write").</param>
        /// <returns><see langword="true"/> if the user is authorized to perform the specified action on the resource; otherwise,
        /// <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the response from the policy decision point (PDP) is null or invalid.</exception>
        public async Task<bool> AuthorizeResourceAccess(string resource, Guid resourceParty, ClaimsPrincipal userPrincipal, string action)
        {
            XacmlJsonRequestRoot request = DecisionHelper.CreateDecisionRequestForResourceRegistryResource(resource, resourceParty, userPrincipal, action);
            XacmlJsonResponse response = await Pdp.GetDecisionForRequest(request);

            if (response?.Response == null)
            {
                throw new InvalidOperationException("response");
            }

            if (!DecisionHelper.ValidatePdpDecision(response.Response, userPrincipal))
            {
                return false;
            }

            return true;
        }
    }
}

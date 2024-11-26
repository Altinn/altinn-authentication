using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Helpers;
#nullable enable

/// <summary>
/// Helper class
/// </summary>
public class DelegationHelper(
    ISystemRegisterService systemRegisterService,
    IAccessManagementClient accessManagementClient)
{
    /// <summary>
    /// Checks Delegation for a user
    /// </summary>
    /// <param name="partyId">reportee</param>
    /// <param name="systemId">the system</param>
    /// <param name="requestedRights">The set of requested Rights, (currently empty collection from the BFF/UI), Request or ChangeRequest </param>
    /// <param name="fromBff">the BFF/UI does not currently support a subset of the Rights, all will be delegated</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>DelegationCheckResult record</returns>
    public async Task<DelegationCheckResult> UserDelegationCheckForReportee(int partyId, string systemId, List<Right> requestedRights, bool fromBff, CancellationToken cancellationToken = default)
    { 
        (bool allVerified, List<Right> verifiedRights) = await VerifySubsetOfRights(requestedRights, systemId, fromBff, cancellationToken);
        if (!allVerified)
        {
            List<DetailExternal> errors = [];

            foreach (var right in verifiedRights)
            {
                Dictionary<string, List<AttributePair>> parameters = [];

                foreach (var attributePair in right.Resource)
                {
                    parameters.Add(attributePair.Id, [attributePair]);
                }

                errors.Add(new DetailExternal()
                {
                    Code = DetailCodeExternal.Unknown,
                    Description = "Unknown Right",
                    Parameters = parameters
                });
            }

            return new DelegationCheckResult(false, null, errors);
        }     

        List<RightResponses> rightResponsesList = [];
        List<DetailExternal> allErrorDetails = [];

        foreach (Right right in verifiedRights)
        {
            DelegationCheckRequest request = new()
            {
                Resource = right.Resource
            };

            List<DelegationResponseData>? rightResponses = await accessManagementClient.CheckDelegationAccess(partyId.ToString(), request);

            if (rightResponses is null)
            {
                return new DelegationCheckResult(false, null, null);
            }

            (bool canDelegate, List<DetailExternal> errors) = ResolveIfHasAccess(rightResponses);

            if (!canDelegate)
            {
                return new DelegationCheckResult(false, null, errors);
            }

            rightResponsesList.Add(new RightResponses(rightResponses));
        }

        if (allErrorDetails.Count > 0)
        {
            return new DelegationCheckResult(false, null, allErrorDetails);
        }

        return new DelegationCheckResult(true, rightResponsesList, null);
    }

    private static (bool CanDelegate, List<DetailExternal> Errors) ResolveIfHasAccess(List<DelegationResponseData> rightResponse)
    {
        List<DetailExternal> errors = [];
        var canDelegate = true;

        foreach (var data in rightResponse)
        {
            if (data.Status != "Delegable")
            {
                errors.AddRange(data.Details);
                canDelegate = false;
            }
        }

        return (canDelegate, errors);
    }

    /// <summary>
    /// Checks that all requested Rights are in the System's list of prepared Rights
    /// </summary>
    /// <param name="rights">the Requested Rights</param>
    /// <param name="systemId">the system id</param>
    /// <param name="fromBff">the BFF/UI does not currently support a subset of the Rights, all will be delegated</param>
    /// <param name="cancellationToken">cancellation </param>
    /// <returns>Either the verified rights, or the set of unknown rights</returns>
    private async Task<(bool AllVerified, List<Right> VerifiedRights)> VerifySubsetOfRights(List<Right> rights, string systemId, bool fromBff, CancellationToken cancellationToken)
    {
        List<Right> rightsInSystem = await systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);

        if (fromBff)
        {
            return (true, rightsInSystem);
        }
        
        List<Right> verifiedRights = [];
        List<Right> unknownRights = [];
        bool allVerified = true;

        foreach (var right in rights)
        {
            foreach (var rightInSystem in rightsInSystem)
            {
                if (Right.Compare(right, rightInSystem))
                {
                    verifiedRights.Add(right);
                }
                else
                {
                    unknownRights.Add(right);
                    allVerified = false;
                }
            }
        }

        if (!allVerified)
        {
            return (false, unknownRights);
        }

        return (allVerified, verifiedRights);
    }
}

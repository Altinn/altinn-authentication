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
    /// <param name="cancellationToken">cancel token</param>
    /// <returns></returns>
    public async Task<DelegationCheckResult> UserDelegationCheckForReportee(int partyId, string systemId, CancellationToken cancellationToken = default)
    {
        List<Right> rights = await systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);
        List<RightResponses> rightResponsesList = [];
        List<DetailExternal> allErrorDetails = [];

        foreach (Right right in rights)
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

            if (!ResolveIfHasAccess(rightResponses))
            {
                allErrorDetails.AddRange(FlattenListOfErrors(rightResponses));
            }

            rightResponsesList.Add(new RightResponses(rightResponses));
        }

        if (allErrorDetails.Count > 0)
        {
            return new DelegationCheckResult(false, null, allErrorDetails);
        }

        return new DelegationCheckResult(true, rightResponsesList, null);
    }

    private static List<DetailExternal> FlattenListOfErrors(List<DelegationResponseData> input)
    {
        List<DetailExternal> errors = [];

        foreach (var delegationResponseData in input)
        {
            if (delegationResponseData.Status != "Delegable")
            {
                errors.AddRange(delegationResponseData.Details);
            }
        }

        return errors;
    }

    private static bool ResolveIfHasAccess(List<DelegationResponseData> rightResponse)
    {
        foreach (var data in rightResponse)
        {
            if (data.Status != "Delegable")
            {                
                return false;
            }
        }

        return true;
    }
}

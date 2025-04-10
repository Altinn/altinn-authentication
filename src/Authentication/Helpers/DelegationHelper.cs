using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
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
            var resource = new List<AttributePair>();
            resource = ConvertAppResourceToOldResourceFormat(right.Resource);

            DelegationCheckRequest request = new()
            {
                Resource = resource
            };

            List<DelegationResponseData>? rightResponses = await accessManagementClient.CheckDelegationAccess(partyId.ToString(), request);

            if (rightResponses is null)
            {
                return new DelegationCheckResult(false, null, null);
            }

            (bool canDelegate, List<DetailExternal> errors) = ResolveIfHasAccess(rightResponses);

            if (!canDelegate)
            {
                return new DelegationCheckResult(false, rightResponsesList, errors);
            }

            rightResponsesList.Add(new RightResponses(rightResponses));
        }

        if (allErrorDetails.Count > 0)
        {
            return new DelegationCheckResult(false, rightResponsesList, allErrorDetails);
        }

        return new DelegationCheckResult(true, rightResponsesList, []);
    }

    /// <summary>
    /// Splits the resourceId into Org and App
    /// </summary>
    /// <param name="resourceId">the id of the resource</param>
    /// <returns>org and app name</returns>
    public static (string Org, string App) SplitResourceId(string resourceId)
    {
        if (resourceId.StartsWith("app_"))
        {
            var parts = resourceId.Split('_', 3);
            if (parts.Length == 3)
            {
                return (parts[1], parts[2]);
            }
        }

        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// Converts the new resource format to old format for app
    /// </summary>
    /// <returns>resource format in old format for app</returns>
    public static List<AttributePair> ConvertAppResourceToOldResourceFormat(List<AttributePair> resource)
    {
        var processedResource = new List<AttributePair>();
        foreach (var attributePair in resource)
        {            
            if (attributePair.Id == AttributeIdentifier.ResourceRegistryAttribute)
            {
                if (!string.IsNullOrEmpty(attributePair.Value) && attributePair.Value.StartsWith("app_"))
                {
                    var (org, app) = SplitResourceId(attributePair.Value);
                    if (!string.IsNullOrEmpty(org) && !string.IsNullOrEmpty(app))
                    {
                        processedResource.Add(new AttributePair { Id = AttributeIdentifier.OrgAttribute, Value = org });
                        processedResource.Add(new AttributePair { Id = AttributeIdentifier.AppAttribute, Value = app });
                    }
                }
                else
                {
                    processedResource.Add(attributePair);
                }
            }
        }

        return processedResource;
    }

    /// <summary>
    /// Maps the DetailExternal list to a ProblemInstance
    /// </summary>
    /// <param name="errors">the error received from access management</param>
    /// <returns></returns>
    public static ProblemInstance MapDetailExternalErrorListToProblemInstance(List<DetailExternal>? errors)
    {
        if (errors is null || errors.Count == 0 || errors[0].Code == DetailCodeExternal.Unknown)
        {
            return Problem.UnableToDoDelegationCheck;
        }

        if (errors[0].Code == DetailCodeExternal.MissingRoleAccess)
        {
            return Problem.DelegationRightMissingRoleAccess;
        }

        if (errors[0].Code == DetailCodeExternal.MissingDelegationAccess)
        {
            return Problem.DelegationRightMissingDelegationAccess;
        }

        if (errors[0].Code == DetailCodeExternal.MissingSrrRightAccess)
        {
            return Problem.DelegationRightMissingSrrRightAccess;
        }

        if (errors[0].Code == DetailCodeExternal.InsufficientAuthenticationLevel)
        {
            return Problem.DelegationRightInsufficientAuthenticationLevel;
        }

        return Problem.UnableToDoDelegationCheck;
    }

    private static (bool CanDelegate, List<DetailExternal> Errors) ResolveIfHasAccess(List<DelegationResponseData> rightResponse)
    {
        List<DetailExternal> errors = [];
        var canDelegate = true;

        foreach (var data in rightResponse)
        {
            if (data.Status != "Delegable")
            {
                if (data.Details is not null && data.Details.Count > 0)
                {
                    errors.AddRange(data.Details);
                }
                else
                {
                    Dictionary<string, List<AttributePair>> parameters = [];

                    foreach (var attributePair in data.Resource)
                    {
                        parameters.Add(attributePair.Id, [attributePair]);
                    }

                    errors.Add(new DetailExternal()
                    {
                        Code = DetailCodeExternal.Unknown,
                        Description = "Unknown Error During DelegationCheck",
                        Parameters = parameters
                    });                    
                }

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
            bool found = false;
            foreach (var rightInSystem in rightsInSystem)
            {
                if (Right.Compare(right, rightInSystem))
                {
                    verifiedRights.Add(right);
                    found = true;
                    break;
                }                
            }

            if (!found)
            {
                unknownRights.Add(right);
                allVerified = false;
            }
        }

        if (!allVerified)
        {
            return (false, unknownRights);
        }

        return (allVerified, verifiedRights);
    }
}

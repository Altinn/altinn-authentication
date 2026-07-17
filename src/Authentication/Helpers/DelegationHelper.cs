using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Telemetry;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Authentication.Helpers;
#nullable enable

/// <summary>
/// Helper class
/// </summary>
public class DelegationHelper(
    ISystemRegisterService systemRegisterService,
    IAccessManagementClient accessManagementClient,
    ILogger<DelegationHelper> logger)
{
    /// <summary>
    /// Parameter key used to carry the display label (friendly name or identifier) of the resource a
    /// not-delegable reason belongs to, so it can be listed in the user-facing delegation reasons.
    /// </summary>
    private const string ResourceLabelParameterKey = "resourceName";

    /// <summary>
    /// Checks Delegation for a user
    /// </summary>
    /// <param name="partyUuid">reportee</param>
    /// <param name="systemId">the system</param>
    /// <param name="requestedRights">The set of requested Rights, (currently empty collection from the BFF/UI), Request or ChangeRequest </param>
    /// <param name="fromBff">the BFF/UI does not currently support a subset of the Rights, all will be delegated</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>DelegationCheckResult record</returns>
    public async Task<DelegationCheckResult> UserDelegationCheckForReportee(Guid partyUuid, string systemId, List<Right> requestedRights, bool fromBff, CancellationToken cancellationToken = default)
    {
        using var activity = AuthenticationTelemetry.StartActivity(
                name: nameof(UserDelegationCheckForReportee),
                tags: [
                    new("system.id", systemId),
                ]);

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
        bool anyResourceNotDelegable = false;

        foreach (Right right in verifiedRights)
        {
            string resourceId = right.Resource.FirstOrDefault(attr => attr.Id == AttributeIdentifier.ResourceRegistryAttribute)?.Value ?? string.Empty;

            ResourceCheckDto? resourceCheckDto = await accessManagementClient.CheckDelegationAccess(partyUuid, resourceId, cancellationToken);
            
            if (resourceCheckDto is null)
            {
                // HTTP failure during the delegation check is already logged with the response body,
                // status code, party and resource in AccessManagementClient.CheckDelegationAccess.
                return new DelegationCheckResult(false, null, null);
            }

            (bool canDelegate, List<DetailExternal> errors, List<string> rightKeys) = ResolveIfHasAccessNew(resourceCheckDto);

            if (!canDelegate)
            {
                anyResourceNotDelegable = true;

                // The delegation check completed (HTTP 200) but the resource right is not delegable.
                // Access Management returns the actual reason in each right's ReasonCodes (e.g.
                // MissingPackageAccess, MissingRoleAccess, MissingDelegationAccess) - not in Permissions -
                // so read ReasonCodes directly to make the failure debuggable in App Insights (issue #2027).
                string reasonDetails = string.Join(
                    " | ",
                    resourceCheckDto.Rights
                        .Where(r => !r.Result)
                        .Select(r =>
                        {
                            string reasonCodes = r.ReasonCodes is not null && r.ReasonCodes.Any()
                                ? string.Join(", ", r.ReasonCodes)
                                : "no reason provided";
                            return $"{r.Right?.Key}: {reasonCodes}";
                        }));

                logger.LogError(
                    "Authentication // DelegationHelper // UserDelegationCheckForReportee // Resource right not delegable // Party: {PartyUuid}, System: {SystemId}, Resource: {Resource}, Reasons: {Reasons}",
                    partyUuid,
                    systemId,
                    resourceId,
                    reasonDetails);

                // Tag each error with the resource it belongs to and keep checking the remaining
                // resources, so the caller can list every resource that could not be delegated -
                // not just the first one that failed. Prefer the friendly resource name returned by
                // Access Management; fall back to the resource identifier when the name is absent.
                string resourceLabel = !string.IsNullOrWhiteSpace(resourceCheckDto.Resource?.Name)
                    ? resourceCheckDto.Resource!.Name
                    : resourceId;

                if (!string.IsNullOrEmpty(resourceLabel))
                {
                    foreach (DetailExternal error in errors)
                    {
                        error.Parameters ??= [];
                        error.Parameters[ResourceLabelParameterKey] =
                            [new AttributePair { Id = ResourceLabelParameterKey, Value = resourceLabel }];
                    }
                }

                allErrorDetails.AddRange(errors);
                continue;
            }

            RightKeyListDto rightKeyList = new RightKeyListDto { DirectRightKeys = rightKeys };

            rightResponsesList.Add(new RightResponses(resourceId, rightKeyList));
        }

        if (anyResourceNotDelegable)
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
    /// Converts the old resource format to new format for app
    /// </summary>
    /// <returns>resource format in old format for app</returns>
    public static List<AttributePair> ConvertOldAppResourceFormatToNewAppResourceFormat(List<AttributePair> resource)
    {
        if (resource.Count == 2)
        {
            var orgAttribute = resource.FirstOrDefault(attr => attr.Id == AttributeIdentifier.OrgAttribute);
            var appAttribute = resource.FirstOrDefault(attr => attr.Id == AttributeIdentifier.AppAttribute);
            if (orgAttribute != null && appAttribute != null)
            {
                return new List<AttributePair>
                {
                    new AttributePair { Id = AttributeIdentifier.ResourceRegistryAttribute, Value = $"app_{orgAttribute.Value}_{appAttribute.Value}" }
                };
            }
        }

        // Not old app format, preserve as-is
        return resource.ToList();
    }

    /// <summary>
    /// Maps the DetailExternal list to a ProblemInstance
    /// </summary>
    /// <param name="errors">the error received from access management</param>
    /// <returns></returns>
    public static ProblemInstance MapDetailExternalErrorListToProblemInstance(List<DetailExternal>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return Problem.UnableToDoDelegationCheck;
        }

        // On a failed delegation check Access Management returns a mix of positive codes (ways the
        // reportee DOES have access, e.g. PackageAccess/RoleAccess) and the blocking code(s) that
        // actually prevent delegation (e.g. ResourceIsMaskinPortenSchema). Only the blocking codes
        // explain the failure, so drive the message off those and ignore the positive ones. If AM sent
        // only non-blocking codes we keep the original list so we never end up with an empty reason.
        List<DetailExternal> blockingErrors = errors.Where(e => IsBlockingReason(e.Code)).ToList();
        if (blockingErrors.Count == 0)
        {
            blockingErrors = errors;
        }

        // A ProblemInstance carries a single headline ErrorCode; use the first blocking reason as the
        // representative. The complete, per-resource breakdown of every blocking reason (all resources,
        // all codes) is carried in the delegationReasons extension below.
        ProblemDescriptor descriptor = blockingErrors[0].Code switch
        {
            DetailCodeExternal.MissingPackageAccess => Problem.DelegationRightMissingPackageAccess,
            DetailCodeExternal.MissingRoleAccess => Problem.DelegationRightMissingRoleAccess,
            DetailCodeExternal.MissingDelegationAccess => Problem.DelegationRightMissingDelegationAccess,
            DetailCodeExternal.MissingSrrRightAccess => Problem.DelegationRightMissingSrrRightAccess,
            DetailCodeExternal.InsufficientAuthenticationLevel => Problem.DelegationRightInsufficientAuthenticationLevel,
            DetailCodeExternal.AccessListValidationFail => Problem.DelegationRightAccessListValidationFail,
            DetailCodeExternal.ResourceNotDelegable => Problem.DelegationRightResourceNotDelegable,
            DetailCodeExternal.ResourceIsMaskinPortenSchema => Problem.DelegationRightResourceIsMaskinPortenSchema,
            _ => Problem.UnableToDoDelegationCheck
        };

        // Group every blocking reason by the resource it belongs to and list its distinct codes, so all
        // reasons from Access Management end up in this one problem instance, e.g.
        // "Resource A: MissingRoleAccess, MissingPackageAccess | Resource B: ResourceIsMaskinPortenSchema".
        string reasons = BuildGroupedDelegationReasons(blockingErrors);

        if (string.IsNullOrWhiteSpace(reasons))
        {
            return descriptor;
        }

        ProblemExtensionData extensionData = ProblemExtensionData.Create(
        [
            new KeyValuePair<string, string>("delegationReasons", reasons)
        ]);

        return descriptor.Create(extensionData);
    }

    /// <summary>
    /// Builds the delegation reasons string, grouped by the resource each reason belongs to and listing the
    /// distinct blocking codes per resource. The reason code is the reliable, mappable signal (the frontend
    /// maps it to localized text); Access Management's free-text description is used only as a fallback for
    /// <see cref="DetailCodeExternal.Unknown"/>, where there is no meaningful code (it is otherwise
    /// unreliable, e.g. "NoAccess").
    /// </summary>
    private static string BuildGroupedDelegationReasons(List<DetailExternal> blockingErrors)
    {
        IEnumerable<string> groups = blockingErrors
            .GroupBy(GetResourceLabel)
            .Select(group =>
            {
                string codes = string.Join(", ", group.Select(FormatReasonCode).Distinct());
                return string.IsNullOrEmpty(group.Key) ? codes : $"{group.Key}: {codes}";
            });

        return string.Join(" | ", groups);
    }

    /// <summary>
    /// Gets the display label (friendly resource name or identifier) tagged onto the error, or an empty
    /// string when the reason is not tied to a specific resource.
    /// </summary>
    private static string GetResourceLabel(DetailExternal error)
    {
        return error.Parameters is not null
            && error.Parameters.TryGetValue(ResourceLabelParameterKey, out List<AttributePair>? resourceAttrs)
                ? resourceAttrs.FirstOrDefault()?.Value ?? string.Empty
                : string.Empty;
    }

    /// <summary>
    /// Formats a single reason as its code, falling back to the free-text description only for
    /// <see cref="DetailCodeExternal.Unknown"/>.
    /// </summary>
    private static string FormatReasonCode(DetailExternal error)
    {
        return error.Code == DetailCodeExternal.Unknown && !string.IsNullOrWhiteSpace(error.Description)
            ? error.Description
            : error.Code.ToString();
    }

    /// <summary>
    /// Returns true for reason codes that actually block delegation. Access Management can return
    /// positive codes (RoleAccess, PackageAccess, ...) alongside a failure, describing how the reportee
    /// does have access; those must not be treated as the reason a right/resource is not delegable.
    /// <see cref="DetailCodeExternal.Unknown"/> is treated as blocking so a genuinely unknown reason
    /// still surfaces instead of being filtered away.
    /// </summary>
    private static bool IsBlockingReason(DetailCodeExternal code) => code switch
    {
        DetailCodeExternal.MissingRoleAccess
        or DetailCodeExternal.MissingDelegationAccess
        or DetailCodeExternal.MissingSrrRightAccess
        or DetailCodeExternal.InsufficientAuthenticationLevel
        or DetailCodeExternal.AccessListValidationFail
        or DetailCodeExternal.MissingPackageAccess
        or DetailCodeExternal.ResourceNotDelegable
        or DetailCodeExternal.ResourceIsMaskinPortenSchema
        or DetailCodeExternal.Unknown => true,
        _ => false
    };

    /// <summary>
    /// Validates delegation rights for a list of access packages for a party
    /// </summary>
    /// <param name="partyId">the id of the party that delegates access</param>
    /// <param name="systemId">the id of the system that the vendor requests access for</param>
    /// <param name="accessPackages">list of accesspackages to be delegated</param>
    /// <param name="fromBff">if the check is for the user driver or vendor driven system user creation</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns></returns>
    public async Task<Result<AccessPackageDelegationCheckResult>> ValidateDelegationRightsForAccessPackages(Guid partyId, string systemId, List<AccessPackage> accessPackages, bool fromBff, CancellationToken cancellationToken = default)
    {
        // 1. Verify that the access packages are valid for the system
        (bool allVerified, List<AccessPackage> validAccessPackages, List<AccessPackage> invalidAccessPackages) = await ValidateRequestedAccessPackages(accessPackages, systemId, fromBff, cancellationToken);

        if (!allVerified)
        {
            var errors = invalidAccessPackages.Select(pkg => new DetailExternal
            {
                Code = DetailCodeExternal.Unknown,
                Description = "Unknown Access Package",
                Parameters = new Dictionary<string, List<AttributePair>>
            {
                { "Urn", new List<AttributePair> { new AttributePair { Id = "Urn", Value = pkg.Urn ?? string.Empty } } }
            }
            }).ToList();

            var problemExtensionData = ProblemExtensionData.Create(new[]
            {
                new KeyValuePair<string, string>("Invalid Urn Details : ", string.Join(" | ", invalidAccessPackages))
            });
            ProblemInstance problemInstance = Problem.AccessPackage_ValidationFailed.Create(problemExtensionData);
            return new Result<AccessPackageDelegationCheckResult>(problemInstance);
        }

        // 2. Check if access packages are delegable
        var urns = validAccessPackages
                        .Where(pkg => !string.IsNullOrEmpty(pkg.Urn))
                        .Select(pkg => pkg.Urn!)
                        .ToArray();

        // Nothing to check - return early only when the validated package list is genuinely empty
        // (e.g. an idempotent approve where every required package is already delegated). This must
        // not key off urns.Length, since that would also short-circuit packages missing a URN and
        // wrongly return them as delegable. Calling the delegation-check API with an empty package
        // filter makes it evaluate EVERY access package in the system, which would never all be delegable.
        if (validAccessPackages.Count == 0)
        {
            return new AccessPackageDelegationCheckResult(true, validAccessPackages);
        }

        var resultList = await accessManagementClient
            .CheckDelegationAccessForAccessPackage(partyId, urns, cancellationToken)
            .ToListAsync(cancellationToken);

        // Check for any problems before further processing
        foreach (var result in resultList)
        {
            if (result.IsProblem)
            {
                // HTTP failure during the delegation check is already logged with the response body,
                // status code, party and packages in AccessManagementClient.CheckDelegationAccessForAccessPackage.
                var problemExtensionData = ProblemExtensionData.Create(new[]
                {
                    new KeyValuePair<string, string>("Problem Detail : ", result.Problem.Detail)
                });
                ProblemInstance problemInstance = Problem.AccessPackage_DelegationCheckFailed.Create(problemExtensionData);
                return new Result<AccessPackageDelegationCheckResult>(problemInstance);
            }
        }

        List<AccessPackageDto.Check> delegationCheckResults = resultList
            .Where(r => r.IsSuccess && r.Value is not null)
            .Select(r => r.Value!)
            .ToList();

        // 3. Process results
        bool canDelegate = delegationCheckResults.All(r => r.Result);

        if (canDelegate)
        {
            // Success on delegation check
            return new AccessPackageDelegationCheckResult(true, validAccessPackages);
        }
        else
        {
            // The delegation check completed (HTTP 200) but one or more access packages are not delegable.
            // Log which packages failed and the reasons returned by Access Management to make this debuggable
            // in App Insights (issue #2027).
            List<AccessPackageDto.Check> notDelegable = delegationCheckResults.Where(r => !r.Result).ToList();

            string notDelegableDetails = string.Join(
                " | ",
                notDelegable.Select(r =>
                {
                    string urn = r.Package?.Urn ?? "unknown";
                    string reasons = r.Reasons is not null && r.Reasons.Any()
                        ? string.Join("; ", r.Reasons.Select(reason => reason.Description))
                        : "no reason provided";
                    return $"{urn}: {reasons}";
                }));

            logger.LogError(
                "Authentication // DelegationHelper // ValidateDelegationRightsForAccessPackages // Access package(s) not delegable // Party: {PartyId}, System: {SystemId}, NotDelegable: {NotDelegable}",
                partyId,
                systemId,
                notDelegableDetails);

            // Carry which package(s) failed and why as a problem extension so the end user (and support)
            // sees the reason directly, 
            ProblemInstance problem = string.IsNullOrWhiteSpace(notDelegableDetails)
                ? Problem.AccessPackage_Delegation_MissingRequiredAccess
                : Problem.AccessPackage_Delegation_MissingRequiredAccess.Create(ProblemExtensionData.Create(
                [
                    new KeyValuePair<string, string>("delegationReasons", notDelegableDetails)
                ]));

            return new Result<AccessPackageDelegationCheckResult>(problem);
        }
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

    private static (bool CanDelegate, List<DetailExternal> Errors, List<string> RightKeys) ResolveIfHasAccessNew(ResourceCheckDto resourceCheckDto)
    {
        List<DetailExternal> errors = [];
        bool canDelegate = false;
        List<string> rightKeys = [];

        foreach (var rightCheckDto in resourceCheckDto.Rights)
        {
            // We only need one right to be delegable for the Delegate step afterwards.
            // Those rights that are not delegable will be added to the error details,
            // but will not block the delegation check if at least one right is delegable.
            if (rightCheckDto.Result)
            {
                canDelegate = true;
                rightKeys.Add(rightCheckDto.Right.Key);
            }

            if (!rightCheckDto.Result)
            {
                if (rightCheckDto.Permissions is not null && rightCheckDto.Permissions.Any())
                {
                    errors.AddRange(rightCheckDto.Permissions.Select(p => new DetailExternal
                    {
                        Code = p.PermisionKey,
                        Description = p.Description,
                        Parameters = []
                    }));
                }
                else if (rightCheckDto.ReasonCodes is not null && rightCheckDto.ReasonCodes.Any())
                {                   
                    errors.AddRange(rightCheckDto.ReasonCodes.Select(code => new DetailExternal
                    {
                        Code = code
                    }));
                }
            }
        }

        return (canDelegate, errors, rightKeys);
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

    private async Task<(bool AllVerified, List<AccessPackage> ValidAccessPackages, List<AccessPackage> InvalidAccessPackages)> ValidateRequestedAccessPackages(List<AccessPackage> requestedAccessPackages, string systemId, bool fromBff, CancellationToken cancellationToken)
    {
        List<AccessPackage> validAccessPackages = [];
        List<AccessPackage> invalidAccessPackages = [];

        List<AccessPackage> systemAccessPackages = await systemRegisterService.GetAccessPackagesForRegisteredSystem(systemId, cancellationToken);

        if (fromBff)
        {
            return (true, systemAccessPackages, invalidAccessPackages);
        }

        foreach (var pkg in requestedAccessPackages)
        {
            if (systemAccessPackages.Any(s => s.Urn == pkg.Urn))
            {
                validAccessPackages.Add(pkg);
            }
            else
            {
                invalidAccessPackages.Add(pkg);
            }
        }

        if (invalidAccessPackages.Count > 0)
        {
            return (false, validAccessPackages, invalidAccessPackages);
        }

        return (true, validAccessPackages, invalidAccessPackages);
    }

    /// <summary>
    /// Gets the Accesspackages that are delegated to the SystemUser
    /// </summary>
    /// <param name="partyUuId">The partyUuid which owns the SystemUser</param>
    /// <param name="systemUserId">The Guid Id for the SystemUser</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns></returns>
    public async Task<Result<List<AccessPackage>>> GetAccessPackagesForSystemUser(Guid partyUuId, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        var packagePermissions = await accessManagementClient.GetAccessPackagesForSystemUser(partyUuId, systemUserId, cancellationToken).ToListAsync(cancellationToken);

        List<PackagePermission> delegations = packagePermissions
            .Where(r => r.IsSuccess && r.Value is not null)
            .Select(r => r.Value!)
            .ToList();

        // 3. Process results
        GetDelegatedPackagesFromDelegations(delegations, out List<AccessPackage> accessPackages);
        return accessPackages;
    }

    private static void GetDelegatedPackagesFromDelegations(
        List<PackagePermission> delegations,
        out List<AccessPackage> accessPackages)
    {
        accessPackages = [];
        foreach (PackagePermission packagePermission in delegations)
        {
            if (packagePermission.Package is not null)
            {
                AccessPackage accessPackage = new AccessPackage();
                accessPackage.Urn = packagePermission.Package.Urn;
                accessPackages.Add(accessPackage);
            }
        }
    }
}

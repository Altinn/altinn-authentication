using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Helpers.Tests
{
    public class DelegationHelperTests
    {
        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_NullOrEmptyErrors_ReturnsUnableToDoDelegationCheck()
        {
            // Arrange
            List<DetailExternal>? errors = null;

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.UnableToDoDelegationCheck, result);
        }

        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_UnknownErrorCode_ReturnsUnableToDoDelegationCheck()
        {
            // Arrange
            var errors = new List<DetailExternal>
            {
                new DetailExternal { Code = DetailCodeExternal.Unknown, Description = "Unknown error" }
            };

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.UnableToDoDelegationCheck, result);
        }

        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_MissingRoleAccess_ReturnsDelegationRightMissingRoleAccess()
        {
            // Arrange
            var errors = new List<DetailExternal>
            {
                new DetailExternal { Code = DetailCodeExternal.MissingRoleAccess, Description = "Missing role access" }
            };

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.DelegationRightMissingRoleAccess, result);
        }

        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_MissingDelegationAccess_ReturnsDelegationRightMissingDelegationAccess()
        {
            // Arrange
            var errors = new List<DetailExternal>
            {
                new DetailExternal { Code = DetailCodeExternal.MissingDelegationAccess, Description = "Missing delegation access" }
            };

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.DelegationRightMissingDelegationAccess, result);
        }

        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_MissingSrrRightAccess_ReturnsDelegationRightMissingSrrRightAccess()
        {
            // Arrange
            var errors = new List<DetailExternal>
            {
                new DetailExternal { Code = DetailCodeExternal.MissingSrrRightAccess, Description = "Missing SRR right access" }
            };

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.DelegationRightMissingSrrRightAccess, result);
        }

        [Fact]
        public void MapDetailExternalErrorListToProblemInstance_InsufficientAuthenticationLevel_ReturnsDelegationRightInsufficientAuthenticationLevel()
        {
            // Arrange
            var errors = new List<DetailExternal>
            {
                new DetailExternal { Code = DetailCodeExternal.InsufficientAuthenticationLevel, Description = "Insufficient authentication level" }
            };

            // Act
            var result = DelegationHelper.MapDetailExternalErrorListToProblemInstance(errors);

            // Assert
            Assert.Equal(Problem.DelegationRightInsufficientAuthenticationLevel, result);
        }

        [Theory]
        [InlineData("app_org_appname", "org", "appname")]
        [InlineData("app_orgname_app", "orgname", "app")]
        [InlineData("invalid_format", "", "")]
        [InlineData("app_", "", "")]
        [InlineData("app_org_", "org", "")]
        public void SplitResourceId_ShouldReturnExpectedResults(string resourceId, string expectedOrg, string expectedApp)
        {
            // Act
            var result = DelegationHelper.SplitResourceId(resourceId);

            // Assert
            Assert.Equal(expectedOrg, result.Org);
            Assert.Equal(expectedApp, result.App);
        }

        [Fact]
        public void ConvertAppResourceToOldResourceFormat_ShouldConvertCorrectly()
        {
            // Arrange
            var resource = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "app_org_appname" }
            };

            var expected = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:org", Value = "org" },
                new AttributePair { Id = "urn:altinn:app", Value = "appname" },
            };

            // Act
            var result = DelegationHelper.ConvertAppResourceToOldResourceFormat(resource);

            // Assert
            Assert.Equal(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Id, result[i].Id);
                Assert.Equal(expected[i].Value, result[i].Value);
            }
        }

        [Fact]
        public async Task ValidateDelegationRightsForAccessPackages_AllValidAndDelegable_ReturnsSuccess()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();

            var accessPackage = new AccessPackage { Urn = "urn:valid" };
            var requested = new List<AccessPackage> { accessPackage };
            var systemPackages = new List<AccessPackage> { accessPackage };

            systemRegisterService
                .Setup(s => s.GetAccessPackagesForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemPackages);

            var compactPackage = new AccessPackageDto.Compact { Urn = accessPackage.Urn };
            var checkResult = new List<AccessPackageDto.Check>
            {
                new() { Package = compactPackage, Result = true }
            };

            accessManagementClient
                .Setup(a => a.CheckDelegationAccessForAccessPackage(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(checkResult.ToAsyncEnumerable());

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.ValidateDelegationRightsForAccessPackages(Guid.NewGuid(), "sys", requested, false, CancellationToken.None);

            // Assert
            Assert.True(result.CanDelegate);
            Assert.NotNull(result.accessPackages);
            Assert.Single(result.accessPackages);
            Assert.Empty(result.errors);
        }

        [Fact]
        public async Task ValidateDelegationRightsForAccessPackages_InvalidPackages_ReturnsError()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();
            var requested = new List<AccessPackage> { new() { Urn = "urn:invalid" } };
            var systemPackages = new List<AccessPackage> { new() { Urn = "urn:valid" } };

            systemRegisterService
                .Setup(s => s.GetAccessPackagesForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemPackages);

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.ValidateDelegationRightsForAccessPackages(Guid.NewGuid(), "sys", requested, false, CancellationToken.None);

            // Assert
            Assert.False(result.CanDelegate);
            Assert.Null(result.accessPackages);
            Assert.NotEmpty(result.errors);
            Assert.Contains(result.errors, e => e.Description.Contains("Unknown Access Package"));
        }

        [Fact]
        public async Task ValidateDelegationRightsForAccessPackages_DelegationNotAllowed_ReturnsError()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();
            var requested = new List<AccessPackage> { new() { Urn = "urn:valid" } };
            var systemPackages = new List<AccessPackage> { new() { Urn = "urn:valid" } };

            systemRegisterService
                .Setup(s => s.GetAccessPackagesForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemPackages);

            var checkResult = new List<AccessPackageDto.Check>
        {
            new() { Package = new(), Result = false, Reasons = new List<AccessPackageDto.Check.Reason> { new() { Description = "Not allowed" } } }
        };

            accessManagementClient
                .Setup(a => a.CheckDelegationAccessForAccessPackage(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(checkResult.ToAsyncEnumerable());

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.ValidateDelegationRightsForAccessPackages(Guid.NewGuid(), "sys", requested, false, CancellationToken.None);

            // Assert
            Assert.False(result.CanDelegate);
            Assert.NotNull(result.accessPackages);
            Assert.NotEmpty(result.errors);
            Assert.Contains(result.errors, e => e.Description.Contains("Delegation not allowed"));
        }

        [Fact]
        public async Task ValidateDelegationRightsForAccessPackages_EmptyInput_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();
            var requested = new List<AccessPackage>();
            var systemPackages = new List<AccessPackage>();

            systemRegisterService
                .Setup(s => s.GetAccessPackagesForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemPackages);

            accessManagementClient
                .Setup(a => a.CheckDelegationAccessForAccessPackage(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncEnumerable.Empty<AccessPackageDto.Check>());

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.ValidateDelegationRightsForAccessPackages(Guid.NewGuid(), "sys", requested, false, CancellationToken.None);

            // Assert
            Assert.True(result.CanDelegate);
            Assert.Empty(result.accessPackages);
            Assert.Empty(result.errors);
        }

        [Fact]
        public async Task ValidateDelegationRightsForAccessPackages_PackageWithNullUrn_SkippedInDelegationCheck()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();
            var requested = new List<AccessPackage> { new() { Urn = null } };
            var systemPackages = new List<AccessPackage> { new() { Urn = null } };

            systemRegisterService
                .Setup(s => s.GetAccessPackagesForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemPackages);

            accessManagementClient
                .Setup(a => a.CheckDelegationAccessForAccessPackage(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncEnumerable.Empty<AccessPackageDto.Check>());

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.ValidateDelegationRightsForAccessPackages(Guid.NewGuid(), "sys", requested, false, CancellationToken.None);

            // Assert
            Assert.True(result.CanDelegate);
            Assert.All(result.accessPackages, p => Assert.Null(p.Urn));
            Assert.Empty(result.errors);
        }

        [Fact]
        public async Task UserDelegationCheckForReportee_AllVerifiedAndDelegable_ReturnsSuccess()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();

            var right = new Right { Action = "read", Resource = new List<AttributePair>() };
            var requestedRights = new List<Right> { right };

            // System contains the requested right
            systemRegisterService
                .Setup(s => s.GetRightsForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Right> { right });

            // Delegation is allowed
            accessManagementClient
                .Setup(a => a.CheckDelegationAccess(It.IsAny<string>(), It.IsAny<DelegationCheckRequest>()))
                .ReturnsAsync(new List<DelegationResponseData>
                {
            new DelegationResponseData { Status = "Delegable" }
                });

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.UserDelegationCheckForReportee(1, "sys", requestedRights, false);

            // Assert
            Assert.True(result.CanDelegate);
            Assert.NotNull(result.RightResponses);
            Assert.Empty(result.errors);
            Assert.Single(result.RightResponses);
        }

        [Fact]
        public async Task UserDelegationCheckForReportee_RightsNotVerified_ReturnsUnknownRightError()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();

            var requestedRight = new Right { Action = "read", Resource = new List<AttributePair>() };
            
            // System has no rights, so verification will fail
            systemRegisterService
                .Setup(s => s.GetRightsForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Right>());

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.UserDelegationCheckForReportee(1, "sys", new List<Right> { requestedRight }, false);

            // Assert
            Assert.False(result.CanDelegate);
            Assert.Null(result.RightResponses);
            Assert.NotEmpty(result.errors);
            Assert.Contains(result.errors, e => e.Description == "Unknown Right");
        }

        [Fact]
        public async Task UserDelegationCheckForReportee_DelegationAccessReturnsNull_ReturnsNullErrors()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();

            var right = new Right { Action = "read", Resource = new List<AttributePair>() };
            systemRegisterService
                .Setup(s => s.GetRightsForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Right> { right });

            accessManagementClient
                .Setup(a => a.CheckDelegationAccess(It.IsAny<string>(), It.IsAny<DelegationCheckRequest>()))
                .ReturnsAsync((List<DelegationResponseData>?)null);

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.UserDelegationCheckForReportee(1, "sys", new List<Right> { right }, false);

            // Assert
            Assert.False(result.CanDelegate);
            Assert.Null(result.RightResponses);
            Assert.Null(result.errors);
        }

        [Fact]
        public async Task UserDelegationCheckForReportee_DelegationNotAllowed_ReturnsError()
        {
            // Arrange
            var systemRegisterService = new Mock<ISystemRegisterService>();
            var accessManagementClient = new Mock<IAccessManagementClient>();

            var right = new Right { Action = "read", Resource = new List<AttributePair>() };
            systemRegisterService
                .Setup(s => s.GetRightsForRegisteredSystem(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Right> { right });

            accessManagementClient
                .Setup(a => a.CheckDelegationAccess(It.IsAny<string>(), It.IsAny<DelegationCheckRequest>()))
                .ReturnsAsync(new List<DelegationResponseData>
                {
                new DelegationResponseData
                {
                    Status = "NotDelegable",
                    Details = new List<DetailExternal>
                    {
                        new DetailExternal { Description = "Delegation denied" }
                    }
                }
                });

            var helper = new DelegationHelper(systemRegisterService.Object, accessManagementClient.Object);

            // Act
            var result = await helper.UserDelegationCheckForReportee(1, "sys", new List<Right> { right }, false);

            // Assert
            Assert.False(result.CanDelegate);
            Assert.NotNull(result.RightResponses);
            Assert.NotEmpty(result.errors);
            Assert.Contains(result.errors, e => e.Description == "Delegation denied");
        }
    }
}

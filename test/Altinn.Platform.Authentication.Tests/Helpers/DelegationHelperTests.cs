using System.Collections.Generic;
using Altinn.Authentication.Core.Problems;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Rights;
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

    }
}

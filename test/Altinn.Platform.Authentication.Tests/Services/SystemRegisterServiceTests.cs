using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Tests.Mocks;

using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    /// <summary>
    /// Tests the resource validation in the system register service.
    /// </summary>
    public class SystemRegisterServiceTests
    {
        private readonly ResourceRegistryClientMock _resourceRegistryClient = new();

        private SystemRegisterService CreateService() => new(
            new Mock<ISystemRegisterRepository>().Object,
            _resourceRegistryClient,
            new Mock<IAccessManagementClient>().Object,
            new Mock<ISystemChangeLogRepository>().Object);

        [Fact]
        public async Task GetInvalidResourceIdsDetailed_ResourceMarkedNotDelegable_ReportedAsNotDelegable()
        {
            SystemRegisterService service = CreateService();
            List<Right> rights = [CreateRight("app_ttd_a1-nondelegable")];

            var (invalidFormat, notFound, unsupportedResourceType, notDelegable) =
                await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None);

            Assert.Empty(invalidFormat);
            Assert.Empty(notFound);
            Assert.Empty(unsupportedResourceType);
            Assert.Equal("app_ttd_a1-nondelegable", Assert.Single(notDelegable));
        }

        [Fact]
        public async Task GetInvalidResourceIdsDetailed_DelegableResource_NoErrors()
        {
            SystemRegisterService service = CreateService();
            List<Right> rights = [CreateRight("ske-krav-og-betalinger")];

            var (invalidFormat, notFound, unsupportedResourceType, notDelegable) =
                await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None);

            Assert.Empty(invalidFormat);
            Assert.Empty(notFound);
            Assert.Empty(unsupportedResourceType);
            Assert.Empty(notDelegable);
        }

        [Fact]
        public async Task GetInvalidResourceIdsDetailed_UnsupportedResourceType_NotReportedAsNotDelegable()
        {
            SystemRegisterService service = CreateService();

            // ttd-am-k6 is a MaskinportenSchema, which is not a supported resource type for a system user.
            List<Right> rights = [CreateRight("ttd-am-k6")];

            var (invalidFormat, notFound, unsupportedResourceType, notDelegable) =
                await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None);

            Assert.Empty(invalidFormat);
            Assert.Empty(notFound);
            Assert.Equal("ttd-am-k6", Assert.Single(unsupportedResourceType));
            Assert.Empty(notDelegable);
        }

        [Fact]
        public async Task GetInvalidResourceIdsDetailed_ResourceTurnedNotDelegable_ReportedAsNotDelegable()
        {
            SystemRegisterService service = CreateService();
            List<Right> rights = [CreateRight("ske-krav-og-betalinger")];

            Assert.Empty((await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None)).NotDelegableResourceIds);

            _resourceRegistryClient.NotDelegableResourceIds.Add("ske-krav-og-betalinger");

            var invalidResourceIds = await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None);
            Assert.Equal("ske-krav-og-betalinger", Assert.Single(invalidResourceIds.NotDelegableResourceIds));
        }

        [Fact]
        public async Task GetInvalidResourceIdsDetailed_ResourceNotInResourceRegistry_ReportedAsNotFound()
        {
            SystemRegisterService service = CreateService();
            List<Right> rights = [CreateRight("ske-krav-og-betalinger-deleted")];

            var invalidResourceIds = await service.GetInvalidResourceIdsDetailed(rights, CancellationToken.None);

            Assert.Equal("ske-krav-og-betalinger-deleted", Assert.Single(invalidResourceIds.NotFoundResourceIds));
            Assert.Empty(invalidResourceIds.NotDelegableResourceIds);
        }

        private static Right CreateRight(string resourceId) => new()
        {
            Resource =
            [
                new AttributePair
                {
                    Id = "urn:altinn:resource",
                    Value = resourceId
                }
            ]
        };
    }
}

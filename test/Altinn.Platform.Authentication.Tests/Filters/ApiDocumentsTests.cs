#nullable enable
using Altinn.Platform.Authentication.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Filters;

/// <summary>
/// The operationId is what generated clients bind to, so the convention that produces it is a
/// published contract rather than an implementation detail.
/// </summary>
public class ApiDocumentsTests
{
    [Fact]
    public void GetOperationId_CombinesControllerAndAction()
    {
        // Generators split this on the underscore into a client class and a method.
        ApiDescription endpoint = new()
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "SystemUserClientDelegation",
                ActionName = "GetClientsDelegatedToSystemUser",
            },
        };

        Assert.Equal(
            "SystemUserClientDelegation_GetClientsDelegatedToSystemUser",
            ApiDocuments.GetOperationId(endpoint));
    }

    [Fact]
    public void GetOperationId_ReturnsNull_ForAnEndpointThatIsNotAControllerAction()
    {
        // Minimal-API and other non-controller endpoints have no controller/action pair to name
        // them by. Swashbuckle omits the operationId rather than inventing one.
        ApiDescription endpoint = new() { ActionDescriptor = new ActionDescriptor() };

        Assert.Null(ApiDocuments.GetOperationId(endpoint));
    }
}

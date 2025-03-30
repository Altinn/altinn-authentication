namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class DelegationResponseDto
{
    public string agentSystemUserId { get; set; }
    public string delegationId { get; set; }
    public string customerId { get; set; }

    public string assignmentId { get; set; }
}
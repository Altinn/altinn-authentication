namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.VendorClientDelegation;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ClientsForDelegationResponseDto
{
    [JsonPropertyName("links")]
    public Dictionary<string, string> Links { get; set; } = new();

    [JsonPropertyName("systemUserInformation")]
    public SystemUserInformationDto SystemUserInformation { get; set; } = default!;

    [JsonPropertyName("data")]
    public List<ClientInfoDto> Data { get; set; } = new();
}

public class SystemUserInformationDto
{
    [JsonPropertyName("systemUserId")]
    public Guid SystemUserId { get; set; }

    [JsonPropertyName("systemUserOwnerOrg")]
    public string SystemUserOwnerOrg { get; set; } = string.Empty;
}

public class ClientInfoDto
{
    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    [JsonPropertyName("clientOrganizationNumber")]
    public string ClientOrganizationNumber { get; set; } = string.Empty;

    [JsonPropertyName("clientOrganizationName")]
    public string ClientOrganizationName { get; set; } = string.Empty;
}
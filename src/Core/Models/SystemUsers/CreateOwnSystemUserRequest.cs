using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Request to create a <see cref="Enums.SystemUserType.Standalone"/> SystemUser: the Registered
/// System and the SystemUser are created together, for an organisation building its own system.
/// The vendor org number and Maskinporten ClientId are not part of the request body — both are
/// taken from the caller's own Maskinporten access token (the <c>consumer</c> and <c>client_id</c>
/// claims), since the calling organisation is both the vendor and the customer.
/// </summary>
[ExcludeFromCodeCoverage]
public class CreateOwnSystemUserRequest
{
    /// <summary>
    /// Display name for the SystemUser, chosen by the caller.
    /// </summary>
    [JsonPropertyName("integrationTitle")]
    public required string IntegrationTitle { get; set; }
}

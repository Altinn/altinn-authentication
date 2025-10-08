#nullable enable

using System;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models;

/// <summary>
/// Represents a party in Altinn Register.
/// </summary>
public class PartyInfo
{
    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    [JsonPropertyName("partyType")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    [JsonPropertyName("personIdentifier")]
    public required string Pid { get; init; }

    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public Guid Uuid { get; init; }

    /// <summary>
    /// Gets the canonical URN of the party.
    /// </summary>
    [JsonPropertyName("urn")]
    public required string Urn { get; init; }

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    [JsonPropertyName("partyId")]
    public uint PartyId { get; init; }

    /// <summary>
    /// Gets the display-name of the party.
    /// </summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the user object associated with the party.
    /// </summary>
    [JsonPropertyName("user")]
    public User? User { get; init; }
}

/// <summary>
/// Represents the user properties from Altinn Register.
/// </summary>
public class User
{
    /// <summary>
    /// Gets the userId of the party.
    /// </summary>
    [JsonPropertyName("userId")]
    public uint UserId { get; init; }

    /// <summary>
    /// Gets the username of the party.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }
}
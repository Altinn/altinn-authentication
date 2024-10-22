// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

/// <summary>
/// Helper class allowing deserialization of content in Resources/jwks
/// </summary>
public class Jwk
{
    public required string? p { get; set; }

    public required string? kty { get; set; }

    public required string? q { get; set; }

    public required string? d { get; set; }

    public required string? e { get; set; }

    public required string? use { get; set; }

    public required string? kid { get; set; }

    public required string? qi { get; set; }

    public required string? dp { get; set; }

    public required string? alg { get; set; }

    public required string? dq { get; set; }

    public required string? n { get; set; }
}
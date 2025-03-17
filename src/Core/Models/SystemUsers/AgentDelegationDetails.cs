using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

// Used together with 
[ExcludeFromCodeCoverage]
public class AgentDelegationDetails
{
    public required string ClientRole { get; set; } // REGN // evt ny std "regnskapsfører"
    public required string AccessPackage { get; set; } // Regnskapsfører med signeringsrett / urn:accesspackage:[...]
}

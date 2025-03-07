using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models;

[ExcludeFromCodeCoverage]
public record class RegisteredSystemDTO
{
    /// <summary>
    /// A unique External Id for this System, in human-readable string format.    
    /// </summary>
    public required string SystemId { get; set; } = string.Empty;

    /// <summary>
    /// Organization number of the system Vendor that offers the product (system)
    /// </summary>
    public required string SystemVendorOrgNumber { get; set; }

    /// <summary>
    /// Organization number of the system Vendor that offers the product (system)
    /// </summary>
    public string SystemVendorOrgName { get; set; } = string.Empty;

    /// <summary>
    /// A short name of the product, used when displaying to the user
    /// </summary>
    public required IDictionary<string, string> Name { get; set; }

    /// <summary>
    /// A short description of the product, used when displaying to the user
    /// </summary>
    public required IDictionary<string, string> Description { get; set; }

    /// <summary>
    /// The array of Rights versus System Provider's Resources needed to use this Registered System
    /// </summary>
    public List<Right> Rights { get; set; } = [];

    /// <summary>
    /// The array of access packages versus System Provider's Resources needed to use this Registered System
    /// </summary>
    public List<AccessPackage> AccessPackages { get; set; } = [];

    /// <summary>
    /// True if the registered system is visible to the user in the UI
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// The system user type
    /// </summary>
    [JsonIgnore]
    public SystemUserType UserType { get; set; }
}
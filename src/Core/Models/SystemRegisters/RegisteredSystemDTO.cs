using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemRegisters;

[ExcludeFromCodeCoverage]
public record class RegisteredSystemDTO
{
    /// <summary>
    /// A unique External Id for this System, in human-readable string format.    
    /// </summary>
    [Required]
    public string SystemId { get; set; } = string.Empty;

    /// <summary>
    /// Organization number of the system Vendor that offers the product (system)
    /// </summary>
    [Required]
    public string SystemVendorOrgNumber { get; set; }

    /// <summary>
    /// Organization number of the system Vendor that offers the product (system)
    /// </summary>
    [Required]
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
}

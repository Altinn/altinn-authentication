using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.SystemRegisters
{
    /// <summary>
    /// Model representing a system change log entry.
    /// </summary>
    public class SystemChangeLog
    {
        public Guid SystemInternalId { get; set; }
        public string? ChangedByOrgNumber { get; set; }
        public SystemChangeType ChangeType { get; set; }
        public object ChangedData { get; set; } = default!;
        public string? ClientId { get; set; }
        public DateTimeOffset? Created { get; set; }
    }
}

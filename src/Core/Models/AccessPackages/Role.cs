using System;
using System.Collections.Generic;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Compact Role Model
    /// </summary>
    public class CompactRole
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Value
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Children. Null for a role without sub-roles — Access Management sends "children": null.
        /// </summary>
        public List<CompactRole>? Children { get; set; }
    }
}

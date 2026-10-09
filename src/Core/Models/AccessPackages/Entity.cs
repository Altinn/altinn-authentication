using System;
using System.Collections.Generic;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Compact Entity Model
    /// </summary>
    public class CompactEntity
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Type
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Variant
        /// </summary>
        public string? Variant { get; set; }

        /// <summary>
        /// Values from entityLoookup. Access Management sends an explicit null for
        /// entities without lookup values (e.g. a system user).
        /// </summary>
        public Dictionary<string, string>? KeyValues { get; set; }

        /// <summary>
        /// Parent. Null for a top-level entity — Access Management sends "parent": null.
        /// </summary>
        public CompactEntity? Parent { get; set; }

        /// <summary>
        /// Children. Null for a leaf entity — Access Management sends "children": null.
        /// </summary>
        public List<CompactEntity>? Children { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Package
    /// </summary>
    public class Package
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// ProviderId
        /// </summary>
        public Guid ProviderId { get; set; }

        /// <summary>
        /// EntityTypeId
        /// </summary>
        public Guid EntityTypeId { get; set; }

        /// <summary>
        /// AreaId
        /// </summary>
        public Guid AreaId { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// True if the package is one of "REGN", "REVI", "Forretningsfører" roller
        /// </summary>
        public bool IsDelegable { get; set; }

        /// <summary>
        /// Has resources
        /// </summary>
        public bool HasResources { get; set; }

        /// <summary>
        /// Urn
        /// </summary>
        public string Urn { get; set; }
    }

    /// <summary>
    /// Compact Package Model
    /// </summary>
    public class CompactPackage
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Urn
        /// </summary>
        public string Urn { get; set; }

        /// <summary>
        /// AreaId
        /// </summary>
        public Guid AreaId { get; set; }
    }
}

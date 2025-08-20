using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Role
    /// </summary>
    public class Role
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// EntityTypeId
        /// e.g Organization
        /// </summary>
        public Guid? EntityTypeId { get; set; }

        /// <summary>
        /// ProviderId
        /// </summary>
        public Guid ProviderId { get; set; }

        /// <summary>
        /// Name
        /// e.g Dagligleder
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Code
        /// e.g daglig-leder
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Description
        /// e.g The main operator of the organization
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Defines the role as a KeyRole
        /// </summary>
        public bool IsKeyRole { get; set; }

        /// <summary>
        /// Urn
        /// e.g altinn:external-role:ccr:daglig-leder
        /// altinn:role:tilgangsstyrer
        /// </summary>
        public string Urn { get; set; }

        /// <summary>
        /// Can be assigned
        /// </summary>
        public bool IsAssignable { get; set; }
    }

    /// <summary>
    /// Extended Role
    /// </summary>
    public class ExtRole : Role
    {
        /// <summary>
        /// EntityType
        /// </summary>
        public EntityType EntityType { get; set; }

        /// <summary>
        /// Provider
        /// </summary>
        public Provider Provider { get; set; }
    }

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
        public string Code { get; set; }

        /// <summary>
        /// Children
        /// </summary>
        public List<CompactRole> Children { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    /// <summary>
    /// A list of customers party records.
    /// </summary>
    public record CustomerList
    {
        /// <summary>
        /// List data wrapper
        /// </summary>
        public List<PartyRecord> Data { get; set; } = [];
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// Model for performing search for resources in the resource registry
    /// </summary>
    public class ResourceSearch
    {
        /// <summary>
        /// ID
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Title
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// ResourceType
        /// </summary>
        public ResourceType? ResourceType { get; set; }

        /// <summary>
        /// Keywords
        /// </summary>
        public string? Keyword { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// Defines a contact point
    /// </summary>
    public class ContactPoint
    {
        /// <summary>
        /// The type of contact point, phone, email ++
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// The contact details. The actual phone number, email adress
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Phone details
        /// </summary>
        public string Telephone { get; set; }

        /// <summary>
        /// Contact page
        /// </summary>
        public string ContactPage { get; set; }
    }
}

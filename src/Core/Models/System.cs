﻿using Altinn.Platform.Authentication.Core.Models;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Altinn.Platform.Authentication.Core.SystemRegister.Models
{
    /// <summary>
    /// Model for the System Register
    /// A Registered System is a product supplied by a System Vendor,
    /// it may need Rights to use or acccess Resources at a Service Provider.
    /// </summary>
    public class System
    {
        /// <summary>
        /// The unique Id for this product, in human-readable string format.
        /// The id is in the format of system_vendor_name_plus_name_chosen_by_them.
        /// 
        /// An Optimistic Concurrency pattern to create new System Types Ids is used,
        /// where the Id of the product is prefixed with the SystemVendor to help with uniqueness.
        /// 
        /// When the SystemVendor tries to register a new system type (product), 
        /// they should be aware of their own previous system names 
        /// when giving the new system it's id.
        /// </summary>
        public string SystemId { get; set; }

        /// <summary>
        /// Which Vendor provides this product
        /// </summary>
        public string SystemVendorName { get; set; } = string.Empty;

        /// <summary>
        /// Which Vendor provides this product
        /// </summary>
        public int SystemVendorOrgNumber { get; set; }

        /// <summary>
        /// A short description of the product, used when filtering the dropdown menu.
        /// </summary>
        public string SystemName { get; set; } = string.Empty;

        /// <summary>
        /// The array of Rights versus System Provider's Resources needed to use this Registered System
        /// </summary>
        public List<Right> Rights { get; set; } = [];

        /// <summary>
        /// Registered Systems can be set to Soft Deleted
        /// </summary>
        public bool SoftDeleted { get; set; } = false;

        /// <summary>
        /// The client Id
        /// </summary>
        public List<Guid> ClientId { get; set; } = [];
    }
}
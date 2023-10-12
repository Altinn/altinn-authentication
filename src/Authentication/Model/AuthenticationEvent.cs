using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Security;
using Altinn.Platform.Authentication.Enum;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// This model describes an authentication event. An authentication event is an action triggered when a user authenticates to altinn
    /// </summary>
    public class AuthenticationEvent
    {
        /// <summary>
        /// Date, time of the authentication event. Set by producer of logevents
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Id of the user that triggered that authentication event 
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Relevant if the event is triggered by enterprise user
        /// </summary>
        public string? SupplierId { get; set; }

        /// <summary>
        /// The type of authentication event
        /// </summary>
        public AuthenticationEventType EventType { get; set; }

        /// <summary>
        /// Relevant if the event is triggered by enterprise user?
        /// </summary>
        public int? OrgNumber { get; set; }

        /// <summary>
        /// The type of authentication used by the user (BankId etc)
        /// </summary>
        public AuthenticationMethod AuthenticationMethod { get; set; }

        /// <summary>
        /// The level of authentication used by the user (1, 2,  etc)
        /// </summary>
        public SecurityLevel AuthenticationLevel { get; set; }

        /// <summary>
        /// The ip address of the cliet request
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// The authentication result
        /// </summary>
        public bool IsAuthenticated { get; set; }
    }
}

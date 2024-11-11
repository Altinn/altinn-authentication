using Altinn.Urn.Json;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Definees a flatten Policy Rule as they are stored in the Resource Registry
/// </summary>
public class PolicyRightsDTO
{
        /// <summary>
        /// Defines the action that the subject is allowed to perform on the resource
        /// </summary>
        public required UrnJsonTypeValue Action { get; init; }

        /// <summary>
        /// The Resource attributes that identy one unique resource 
        /// </summary>
        public required List<UrnJsonTypeValue> Resource { get; init; }

        /// <summary>
        /// List of subjects that is allowed to perform the action on the resource
        /// </summary>
        public required List<PolicySubjectDTO> Subjects { get; init; }

        /// <summary>
        /// Returns the right key for the right part of policy resource action
        /// </summary>
        public string RightKey { get; init; }

        /// <summary>
        /// Returns a list of subject types that is allowed to perform the action on the resource
        /// IS used for filtering the 
        /// </summary>
        public List<string> SubjectTypes { get; init; }
}

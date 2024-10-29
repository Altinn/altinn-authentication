using Altinn.Urn.Json;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Defines a  Policy Subject, as they are used in the Resource Registry
/// </summary>
public class PolicySubjectDTO
{
        /// <summary>
        /// Subject attributes that defines the subject
        /// </summary>
        public required IReadOnlyList<UrnJsonTypeValue> SubjectAttributes { get; init; }
}

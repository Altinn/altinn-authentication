namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// Model representation of Competent Authority part of the ServiceResource model
    /// </summary>
    public class CompetentAuthority
    {
        /// <summary>
        /// The organization number. Present on every observed competent authority.
        /// </summary>
        public string Organization { get; set; } = null!;

        /// <summary>
        /// The organization code. Present on every observed competent authority.
        /// </summary>
        public string Orgcode { get; set; } = null!;

        /// <summary>
        /// The organization name. If not set it will be retrieved from register based on the
        /// organization number - and it is genuinely absent from some resource payloads.
        /// </summary>
        public Dictionary<string, string>? Name { get; set; }
    }
}

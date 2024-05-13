namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations
{
    /// <summary>
    /// DTO used when serializing DefaultRight json to and from the db
    /// </summary>
    internal class SystemRegisterRightsJson
    {
        /// <summary>
        /// The json payload
        /// </summary>
        public string JsonText { get; set; }
    }
}

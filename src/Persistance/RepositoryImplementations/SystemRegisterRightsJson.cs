namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations
{
    /// <summary>
    /// DTO used when serializing DefaultRights json to the db
    /// </summary>
    internal class SystemRegisterRightsJson
    {
        /// <summary>
        /// The json payload
        /// </summary>
        public string JsonText { get; set; }
    }
}

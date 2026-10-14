namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Class describing a users profile setting preferences.
    /// </summary>
    public class ProfileSettingPreference
    {
        /// <summary>
        /// Sets the user's language preference in Altinn.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "S2376:Write-only properties should not be used",
            Justification = "Write-only alias used to support incoming JSON 'languageType' while avoiding duplicate serialization output. Value is stored in Language.")]
        public string LanguageType
        {
            set
            {
                Language = value;
            }
        }

        /// <summary>
        /// Gets or sets the user's language preference in Altinn.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets the user's preselected party.
        /// </summary>
        /// <remarks>
        /// This is being phased out in favor of PreselectedPartyUuid.
        /// </remarks>
        public int PreSelectedPartyId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the users want
        /// to be asked for the party on every form submission.
        /// </summary>
        public bool DoNotPromptForParty { get; set; }

        /// <summary>
        /// The UUID of the preselected party. Optional.
        /// </summary>
        public Guid? PreselectedPartyUuid { get; set; }

        /// <summary>
        /// Indicates whether client units should be shown.
        /// </summary>
        public bool ShowClientUnits { get; set; }

        /// <summary>
        /// Indicates whether sub-entities should be shown.
        /// </summary>
        public bool ShouldShowSubEntities { get; set; }

        /// <summary>
        /// Indicates whether deleted entities should be shown.
        /// </summary>
        public bool ShouldShowDeletedEntities { get; set; }
    }
}
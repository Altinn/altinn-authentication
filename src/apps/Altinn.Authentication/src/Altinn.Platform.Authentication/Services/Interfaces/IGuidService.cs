namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Defines interface for generateing guid
    /// </summary>
    public interface IGuidService
    {
        /// <summary>
        /// Generates  a new uuid
        /// </summary>
        /// <returns></returns>
        public string NewGuid();
    }
}

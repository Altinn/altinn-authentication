namespace Mockporten.Services.Interfaces
{
    /// <summary>
    /// Outcome of a shared-access-password check.
    /// </summary>
    public enum SharedPasswordResult
    {
        /// <summary>The supplied password matched.</summary>
        Success,

        /// <summary>The supplied password did not match (or none configured).</summary>
        InvalidPassword,

        /// <summary>Too many failed attempts; login is temporarily locked out.</summary>
        LockedOut
    }

    /// <summary>
    /// Validates the single shared access password that grants the right to use
    /// the Test-IDP. There is no per-user credential. Implementations must use a
    /// constant-time comparison and enforce lockout. See issue #1983 / #1409.
    /// </summary>
    public interface ISharedAccessPasswordValidator
    {
        SharedPasswordResult Validate(string provided);
    }
}

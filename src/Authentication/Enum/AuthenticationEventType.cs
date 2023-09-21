namespace Altinn.Platform.Authentication.Enum
{
    /// <summary>
    /// Enumeration for authentication event types
    /// </summary>
    public enum AuthenticationEventType
    {
        AuthenticationFailed,
        Authenticated,
        Refresh,
        TokenExchange,
        Logout,
    }
}

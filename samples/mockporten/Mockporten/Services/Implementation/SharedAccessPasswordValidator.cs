using System;
using System.Security.Cryptography;
using System.Text;
using Mockporten.Configuration;
using Mockporten.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Mockporten.Services.Implementation
{
    /// <summary>
    /// Validates the single shared access password using a constant-time
    /// comparison, with a global lockout (the secret is shared, so failed
    /// attempts are counted globally - it is the single brute-force target).
    /// Stateless w.r.t. persistence; lockout state is in-memory and this service
    /// must be registered as a singleton.
    /// </summary>
    public class SharedAccessPasswordValidator : ISharedAccessPasswordValidator
    {
        private readonly GeneralSettings _settings;
        private readonly object _gate = new();

        private int _consecutiveFailures;
        private DateTimeOffset? _lockedUntil;

        public SharedAccessPasswordValidator(IOptions<GeneralSettings> settings)
        {
            _settings = settings.Value;
        }

        public SharedPasswordResult Validate(string provided)
        {
            lock (_gate)
            {
                if (_lockedUntil.HasValue)
                {
                    if (DateTimeOffset.UtcNow < _lockedUntil.Value)
                    {
                        return SharedPasswordResult.LockedOut;
                    }

                    // Lockout window elapsed - reset.
                    _lockedUntil = null;
                    _consecutiveFailures = 0;
                }

                if (IsMatch(provided))
                {
                    _consecutiveFailures = 0;
                    return SharedPasswordResult.Success;
                }

                _consecutiveFailures++;
                if (_consecutiveFailures >= _settings.SharedPasswordMaxFailures)
                {
                    _lockedUntil = DateTimeOffset.UtcNow.AddMinutes(_settings.SharedPasswordLockoutMinutes);
                }

                return SharedPasswordResult.InvalidPassword;
            }
        }

        private bool IsMatch(string provided)
        {
            string expected = _settings.TestIdpSharedPassword;

            // Fail-closed: an unconfigured password rejects everything. Still run
            // a fixed-time comparison against a non-empty buffer so the empty and
            // non-empty cases take the same path.
            if (string.IsNullOrEmpty(expected) || provided is null)
            {
                return false;
            }

            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] providedBytes = Encoding.UTF8.GetBytes(provided);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
    }
}

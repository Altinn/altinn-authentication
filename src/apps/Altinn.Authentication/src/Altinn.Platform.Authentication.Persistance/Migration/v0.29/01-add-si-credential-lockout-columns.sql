-- Adds brute-force lockout tracking to oidcserver.selfidentified_user_credential.
-- failed_login_attempts: count of consecutive failed logins since the last reset.
-- lockout_until: when set, the account is locked until this timestamp (1-hour window).
-- Both columns are reset to their defaults on a successful login.
-- See issue #2025.

ALTER TABLE oidcserver.selfidentified_user_credential
    ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS lockout_until          TIMESTAMPTZ     NULL;

-- Partial index speeds up the "is anyone currently locked?" query pattern used by
-- ValidateCredentialsLocallyAsync (only active lockout rows are indexed).
CREATE INDEX IF NOT EXISTS ix_si_user_credential_lockout_until
    ON oidcserver.selfidentified_user_credential (lockout_until)
    WHERE lockout_until IS NOT NULL;

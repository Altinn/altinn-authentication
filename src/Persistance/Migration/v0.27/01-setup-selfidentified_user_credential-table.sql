-- Table: oidcserver.selfidentified_user_credential
-- Holds Altinn 2 self-identified (SI) user credentials migrated into Altinn 3, so SI
-- logins can be validated locally (SHA1 + salt) instead of via SBL Bridge (authentication/api/siuser).
-- See issue #2025.

CREATE TABLE IF NOT EXISTS oidcserver.selfidentified_user_credential (
    -- Altinn 3 identity (returned by ValidateCredentials, used to mint the token)
    party_uuid       UUID         NOT NULL,
    user_id          INTEGER      NOT NULL,   -- legacy, == oidc_session.subject_user_id
    user_name        TEXT         NOT NULL,   -- == oidc_session.subject_user_name; login key

    -- Credentials, copied verbatim from Altinn 2 (no reset required)
    password_hash    TEXT         NOT NULL,   -- Base64 SHA1
    salt             TEXT         NOT NULL,   -- Base64
    password_expiry  TIMESTAMPTZ  NOT NULL,

    -- Status / bookkeeping
    is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
    altinn2_user_id  INTEGER      NULL,       -- source ref for traceability (AUTHN_UserProfile.uid)
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_si_user_credential PRIMARY KEY (user_id),
    CONSTRAINT uq_si_user_credential_username UNIQUE (user_name)
);

CREATE INDEX IF NOT EXISTS ix_si_user_credential_party_uuid
    ON oidcserver.selfidentified_user_credential (party_uuid);

CREATE TABLE IF NOT EXISTS oidcserver.oidc_session (
  sid                  TEXT PRIMARY KEY,
  -- Upstream identity
  upstream_issuer      TEXT NOT NULL,
  upstream_sub         TEXT NOT NULL,		-- OIDC 'sub' from upstream
  subject_id           TEXT,				-- urn:altinn:party:uuid:{subject_party_uuid} 
  external_id          TEXT,					-- upstream-derived external id: PID/email/etc (your policy)
  session_handle_hash BYTEA, 

  -- Altinn identity mapping
  subject_party_uuid   UUID,               -- main Altinn ID
  subject_party_id     INTEGER,            -- legacy
  subject_user_id      INTEGER,            -- legacy
  subject_user_name	   TEXT,               -- legacy

  -- Auth properties
  provider             TEXT NOT NULL,      -- 'idporten'|'uidp'|'testidp'...
  acr                  TEXT,
  auth_time            TIMESTAMPTZ,
  amr                  TEXT[],
  scopes               TEXT[] NOT NULL,
  custom_claims			JSONB NULL,

  -- Lifecycle
  created_at           TIMESTAMPTZ NOT NULL,
  updated_at           TIMESTAMPTZ NOT NULL,
  last_seen_at         TIMESTAMPTZ,
  expires_at           TIMESTAMPTZ,

  -- Logout binding
  upstream_session_sid TEXT,

  -- Diagnostics
  created_by_ip        INET,
  user_agent_hash      TEXT
);

CREATE INDEX IF NOT EXISTS idx_oidc_session_subject_party_uuid ON oidcserver.oidc_session (subject_party_uuid);
CREATE INDEX IF NOT EXISTS idx_oidc_session_subject_party_id   ON oidcserver.oidc_session (subject_party_id);
CREATE INDEX IF NOT EXISTS idx_oidc_session_subject_user_id    ON oidcserver.oidc_session (subject_user_id);
CREATE INDEX IF NOT EXISTS idx_oidc_session_provider_sub       ON oidcserver.oidc_session (provider, upstream_sub);
CREATE INDEX IF NOT EXISTS idx_oidc_session_expires            ON oidcserver.oidc_session (expires_at);
CREATE UNIQUE INDEX IF NOT EXISTS uq_oidc_session_handle
  ON oidcserver.oidc_session (session_handle_hash)
  WHERE session_handle_hash IS NOT NULL;
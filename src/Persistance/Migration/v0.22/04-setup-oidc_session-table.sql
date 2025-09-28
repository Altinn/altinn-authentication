CREATE TABLE IF NOT EXISTS oidcserver.oidc_session (
  sid                  TEXT PRIMARY KEY,
  -- Upstream identity
  upstream_issuer      TEXT NOT NULL,
  upstream_sub         TEXT NOT NULL,      -- OIDC 'sub' from upstream
  subject_id           TEXT,               -- upstream-derived external id: PID/email/etc (your policy)

  -- Altinn identity mapping
  subject_party_uuid   UUID,               -- main Altinn ID
  subject_party_id     INTEGER,            -- legacy
  subject_user_id      INTEGER,            -- legacy

  -- Auth properties
  provider             TEXT NOT NULL,      -- 'idporten'|'uidp'|'testidp'...
  acr                  TEXT,
  auth_time            TIMESTAMPTZ,
  amr                  TEXT[],

  -- Lifecycle
  created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
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

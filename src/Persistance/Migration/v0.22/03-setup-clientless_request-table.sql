-- Ensure schema
CREATE SCHEMA IF NOT EXISTS oidcserver;

-- Minimal record for clientless (legacy) OIDC starts
CREATE TABLE IF NOT EXISTS oidcserver.clientless_request (
  -- Identity & lifecycle
  request_id               UUID PRIMARY KEY,                         -- server-generated
  status                   TEXT NOT NULL DEFAULT 'pending',          -- 'pending'|'completed'|'cancelled'|'error'
  created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at               TIMESTAMPTZ NOT NULL,                     -- e.g. NOW() + interval '10 minutes'
  completed_at             TIMESTAMPTZ,                              -- when finished (success or error)

  -- Inputs (the only things the legacy caller provides)
  issuer                   TEXT NOT NULL,                            -- 'iss' (e.g. 'idporten')
  goto_url                 TEXT NOT NULL,                            -- normalized/validated (relative or https absolute)

  -- Link to the upstream OIDC transaction where state/nonce/pkce are stored
  upstream_request_id      UUID UNIQUE,                              -- set after you create the upstream txn

  -- Diagnostics / correlation (no PII)
  created_by_ip            INET,
  user_agent_hash          TEXT,                                     -- hashed UA
  correlation_id           UUID,

  -- Optional: which callback actually handled it (useful during sunset)
  handled_by_callback      TEXT,                                     -- e.g. '/oidc/callback'

  -- Constraints
  CONSTRAINT chk_clientless_status
    CHECK (status IN ('pending','completed','cancelled','error')),
  CONSTRAINT chk_clientless_times
    CHECK (expires_at > created_at),

  -- DB-level shape check; host allowlist enforcement stays in code
  CONSTRAINT chk_clientless_goto_shape
    CHECK (
      goto_url ~ '^/([^/].*)?$'                           -- relative path starting with single slash (no scheme)
      OR goto_url ~ '^(?i:https)://[^\\s]+'               -- or absolute https URL
    )
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_clientless_expires
  ON oidcserver.clientless_request (expires_at);

CREATE INDEX IF NOT EXISTS idx_clientless_active
  ON oidcserver.clientless_request (expires_at)
  WHERE status = 'pending';

CREATE INDEX IF NOT EXISTS idx_clientless_corr
  ON oidcserver.clientless_request (correlation_id);

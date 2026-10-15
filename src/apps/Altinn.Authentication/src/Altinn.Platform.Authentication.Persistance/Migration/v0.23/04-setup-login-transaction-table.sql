-- Ensure schema
CREATE SCHEMA IF NOT EXISTS oidcserver;

-- Downstream login transaction (Altinn ← client)
CREATE TABLE IF NOT EXISTS oidcserver.login_transaction (
  -- Identity & lifecycle
  request_id               UUID PRIMARY KEY,                         -- server-generated
  status                   TEXT NOT NULL DEFAULT 'pending',          -- 'pending'|'completed'|'cancelled'|'error'
  created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at               TIMESTAMPTZ NOT NULL,                     -- e.g. NOW() + interval '10 minutes'
  completed_at             TIMESTAMPTZ,                              -- set when flow finishes (success or error)

  -- Client & redirect binding
  client_id                TEXT NOT NULL,
  redirect_uri             TEXT NOT NULL,                            -- exact match of client-registered URI

  -- OIDC request parameters (normalized)
  scopes                   TEXT[] NOT NULL,                          -- e.g. '{openid,profile,email}'
  state                    TEXT NOT NULL,
  nonce                    TEXT,                                     -- recommended required; nullable if policy allows
  acr_values               TEXT[],                                   -- e.g. '{idporten-loa-substantial}'
  prompts                  TEXT[],                                   -- e.g. '{login,consent}'
  ui_locales               TEXT[],                                   -- e.g. '{nb,nn,en}'
  max_age                  INTEGER,                                   -- seconds, >= 0

  -- PKCE (downstream)
  code_challenge           TEXT NOT NULL,
  code_challenge_method    TEXT NOT NULL DEFAULT 'S256',

  -- Optional advanced inputs
  request_uri              TEXT,                                     -- PAR (if used)
  request_object_jwt       TEXT,                                     -- JAR raw JWT if you choose to store it
  authorization_details    JSONB,                                    -- future-proof (GNAP-style / fine-grained)

  -- Correlation, diagnostics (no PII)
  created_by_ip            INET,
  user_agent_hash          TEXT,                                     -- hashed UA string to avoid PII
  correlation_id           UUID,

  -- Link to upstream record (created later in step 6; nullable until known)
  upstream_request_id      UUID,

  -- Constraints
  CONSTRAINT fk_login_tx_client
    FOREIGN KEY (client_id) REFERENCES oidcserver.client (client_id),
  CONSTRAINT chk_status
    CHECK (status IN ('pending','completed','cancelled','error')),
  CONSTRAINT chk_times_login_tx
    CHECK (expires_at > created_at),
  CONSTRAINT chk_code_challenge_method_s256
    CHECK (code_challenge_method = 'S256')
);

-- Helpful indexes
-- Fast lookups by client + state (to echo state back or abort flows)
CREATE INDEX IF NOT EXISTS idx_login_tx_client_state
  ON oidcserver.login_transaction (client_id, state);

-- Expiration sweeps
CREATE INDEX IF NOT EXISTS idx_login_tx_expires
  ON oidcserver.login_transaction (expires_at);

-- Hot path: pending + range on expires_at (fixed)
CREATE INDEX IF NOT EXISTS idx_login_tx_active
  ON oidcserver.login_transaction (expires_at)
  WHERE status = 'pending';

-- Correlation & upstream joins
CREATE INDEX IF NOT EXISTS idx_login_tx_corr
  ON oidcserver.login_transaction (correlation_id);

CREATE INDEX IF NOT EXISTS idx_login_tx_upstream
  ON oidcserver.login_transaction (upstream_request_id);

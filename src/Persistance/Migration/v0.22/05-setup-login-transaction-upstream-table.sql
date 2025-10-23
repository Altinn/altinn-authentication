-- Ensure schema exists
CREATE SCHEMA IF NOT EXISTS oidcserver;

-- Upstream login transaction (Altinn → IdP)
CREATE TABLE IF NOT EXISTS oidcserver.login_transaction_upstream (
  upstream_request_id        UUID PRIMARY KEY,                         -- server-generated

  -- Exactly one of these must be non-null:
  request_id                 UUID NULL,                                -- FK → login_transaction.request_id
  unregistered_client_request_id      UUID NULL,                                -- FK → unregistered_client_request.request_id

  status                     TEXT NOT NULL DEFAULT 'pending',          -- 'pending'|'callback_received'|'token_exchanged'|'completed'|'error'|'cancelled'
  created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at                 TIMESTAMPTZ NOT NULL,
  completed_at               TIMESTAMPTZ,

  -- Which upstream IdP/client and endpoints we used
  provider                   TEXT NOT NULL,
  upstream_client_id         TEXT NOT NULL,

  -- Our upstream redirect (Altinn’s callback registered at IdP)
  upstream_redirect_uri      TEXT NOT NULL,

  -- Effective upstream request params
  state                      TEXT NOT NULL,
  nonce                      TEXT NOT NULL,
  scopes                     TEXT[] NOT NULL,
  acr_values                 TEXT[],
  prompts                    TEXT[],
  ui_locales                 TEXT[],
  max_age                    INTEGER,

  -- PKCE (upstream)
  code_verifier              TEXT NOT NULL,
  code_challenge             TEXT NOT NULL,
  code_challenge_method      TEXT NOT NULL DEFAULT 'S256',

  -- Callback data from upstream authorize
  auth_code                  TEXT,
  auth_code_received_at      TIMESTAMPTZ,
  error                      TEXT,
  error_description          TEXT,

  -- Results from upstream token exchange
  token_exchanged_at         TIMESTAMPTZ,
  upstream_issuer            TEXT,
  upstream_sub               TEXT,
  upstream_acr               TEXT,
  upstream_auth_time         TIMESTAMPTZ,
  upstream_id_token_jti      TEXT,
  upstream_session_sid       TEXT,

  -- Correlation/diagnostics (no PII)
  correlation_id             UUID,
  created_by_ip              INET,
  user_agent_hash            TEXT,

  -- Constraints
  CONSTRAINT fk_up_login_tx
    FOREIGN KEY (request_id)
    REFERENCES oidcserver.login_transaction (request_id)
    ON DELETE CASCADE,

  CONSTRAINT fk_up_unregistered_client_tx
    FOREIGN KEY (unregistered_client_request_id)
    REFERENCES oidcserver.unregistered_client_request (request_id)
    ON DELETE CASCADE,

  -- Enforce "exactly one of request_id or unregistered_client__request_id is set"
  CONSTRAINT chk_up_downstream_xor
    CHECK (num_nonnulls(request_id, unregistered_client_request_id) = 1),

  CONSTRAINT chk_up_status
    CHECK (status IN ('pending','callback_received','token_exchanged','completed','error','cancelled')),

  CONSTRAINT chk_up_times
    CHECK (expires_at > created_at),

  CONSTRAINT chk_up_cc_method
    CHECK (code_challenge_method = 'S256')
);

-- Helpful indexes

-- Fast join/lookup by downstream origin
CREATE INDEX IF NOT EXISTS idx_up_request_id
  ON oidcserver.login_transaction_upstream (request_id);
CREATE INDEX IF NOT EXISTS idx_up_unregistered_client_request_id
  ON oidcserver.login_transaction_upstream (unregistered_client_request_id);

-- Optional: ensure at most one upstream txn per downstream origin
CREATE UNIQUE INDEX IF NOT EXISTS uq_up_per_request
  ON oidcserver.login_transaction_upstream (request_id)
  WHERE request_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_up_per_unregistered_client
  ON oidcserver.login_transaction_upstream (unregistered_client_request_id)
  WHERE unregistered_client_request_id IS NOT NULL;

-- Find by upstream state quickly (callback hot path)
CREATE UNIQUE INDEX IF NOT EXISTS uq_up_state_active
  ON oidcserver.login_transaction_upstream (state)
  WHERE status IN ('pending','callback_received');

-- Expiration/active
CREATE INDEX IF NOT EXISTS idx_up_expires
  ON oidcserver.login_transaction_upstream (expires_at);

CREATE INDEX IF NOT EXISTS idx_up_active
  ON oidcserver.login_transaction_upstream (expires_at)
  WHERE status = 'pending';

CREATE INDEX IF NOT EXISTS idx_up_provider
  ON oidcserver.login_transaction_upstream (provider);

CREATE INDEX IF NOT EXISTS idx_up_corr
  ON oidcserver.login_transaction_upstream (correlation_id);

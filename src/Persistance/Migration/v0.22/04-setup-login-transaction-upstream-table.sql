-- Ensure schema exists
CREATE SCHEMA IF NOT EXISTS oidcserver;

-- Upstream login transaction (Altinn → IdP)
CREATE TABLE IF NOT EXISTS oidcserver.login_transaction_upstream (
  -- Identity & lifecycle
  upstream_request_id        UUID PRIMARY KEY,                         -- server-generated
  request_id                 UUID NOT NULL,                            -- FK → downstream login_transaction.request_id
  status                     TEXT NOT NULL DEFAULT 'pending',          -- 'pending'|'callback_received'|'token_exchanged'|'completed'|'error'|'cancelled'
  created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at                 TIMESTAMPTZ NOT NULL,                     -- e.g. NOW() + interval '10 minutes'
  completed_at               TIMESTAMPTZ,

  -- Which upstream IdP/client and endpoints we used
  provider                   TEXT NOT NULL,                            -- e.g. 'idporten'|'feide'|'testidp'
  upstream_client_id         TEXT NOT NULL,
  authorization_endpoint     TEXT NOT NULL,
  token_endpoint             TEXT NOT NULL,
  jwks_uri                   TEXT,                                     -- if you want to pin at creation-time (optional)

  -- Our upstream redirect (Altinn’s callback registered at IdP)
  upstream_redirect_uri      TEXT NOT NULL,

  -- Effective upstream request params
  state                      TEXT NOT NULL,                            -- upstream_state we generated
  nonce                      TEXT NOT NULL,                            -- upstream_nonce we generated
  scopes                     TEXT[] NOT NULL,                          -- typically '{openid,...}'
  acr_values                 TEXT[],                                   -- forwarded/mapped from downstream
  prompts                    TEXT[],
  ui_locales                 TEXT[],
  max_age                    INTEGER,

  -- PKCE (upstream)
  code_verifier              TEXT NOT NULL,                             -- 43–128, charset [A-Za-z0-9-._~]
  code_challenge             TEXT NOT NULL,
  code_challenge_method      TEXT NOT NULL DEFAULT 'S256',

  -- Callback data from upstream authorize
  auth_code                  TEXT,                                      -- code received from IdP
  auth_code_received_at      TIMESTAMPTZ,
  error                      TEXT,                                      -- e.g. access_denied
  error_description          TEXT,

  -- Results from upstream token exchange
  token_exchanged_at         TIMESTAMPTZ,
  upstream_issuer            TEXT,                                      -- iss from ID-token
  upstream_sub               TEXT,                                      -- sub from ID-token
  upstream_acr               TEXT,                                      -- acr from ID-token
  upstream_auth_time         TIMESTAMPTZ,                               -- auth_time → TIMESTAMP
  upstream_id_token_jti      TEXT,                                      -- for optional replay checks
  upstream_session_sid       TEXT,                                      -- sid for front/back channel logout (if provided)

  -- Correlation/diagnostics (no PII)
  correlation_id             UUID,
  created_by_ip              INET,
  user_agent_hash            TEXT,

  -- Constraints
  CONSTRAINT fk_up_login_tx
    FOREIGN KEY (request_id) REFERENCES oidcserver.login_transaction (request_id)
      ON DELETE CASCADE,
  CONSTRAINT chk_up_status
    CHECK (status IN ('pending','callback_received','token_exchanged','completed','error','cancelled')),
  CONSTRAINT chk_up_times
    CHECK (expires_at > created_at),
  CONSTRAINT chk_up_cc_method
    CHECK (code_challenge_method = 'S256')
);

-- Helpful indexes

-- Join + lookup by downstream request
CREATE INDEX IF NOT EXISTS idx_up_login_tx_request
  ON oidcserver.login_transaction_upstream (request_id);

-- Find by upstream state quickly (callback hot path)
CREATE UNIQUE INDEX IF NOT EXISTS uq_up_login_tx_state_pending
  ON oidcserver.login_transaction_upstream (state)
  WHERE status IN ('pending','callback_received');

-- Expiration sweeps / active queries (avoid NOW() in predicate)
CREATE INDEX IF NOT EXISTS idx_up_login_tx_expires
  ON oidcserver.login_transaction_upstream (expires_at);

-- Filter “active” by status; range by expires_at in queries
CREATE INDEX IF NOT EXISTS idx_up_login_tx_active
  ON oidcserver.login_transaction_upstream (expires_at)
  WHERE status = 'pending';

-- By provider if you need ops dashboards
CREATE INDEX IF NOT EXISTS idx_up_login_tx_provider
  ON oidcserver.login_transaction_upstream (provider);

-- Correlation
CREATE INDEX IF NOT EXISTS idx_up_login_tx_corr
  ON oidcserver.login_transaction_upstream (correlation_id);

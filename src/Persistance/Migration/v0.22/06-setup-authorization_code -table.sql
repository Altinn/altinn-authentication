-- Ensure schema exists
CREATE SCHEMA IF NOT EXISTS oidcserver;

-- ===== Authorization code table =====
CREATE TABLE IF NOT EXISTS oidcserver.authorization_code (
  code                   TEXT PRIMARY KEY,
  client_id              TEXT NOT NULL,
  subject_id             TEXT NOT NULL,
  subject_party_uuid     UUID,
  subject_party_id       INTEGER,
  subject_user_id        INTEGER,
  session_id             TEXT NOT NULL,             -- FK to oidc_session.sid (optional FK)
  redirect_uri           TEXT NOT NULL,
  scopes                 TEXT[] NOT NULL,
  nonce                  TEXT,
  acr                    TEXT,
  auth_time              TIMESTAMPTZ,
  code_challenge         TEXT NOT NULL,
  code_challenge_method  TEXT NOT NULL DEFAULT 'S256',
  issued_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at             TIMESTAMPTZ NOT NULL,
  used                   BOOLEAN NOT NULL DEFAULT FALSE,
  used_at                TIMESTAMPTZ,
  created_by_ip          INET,
  correlation_id         UUID,
  CONSTRAINT chk_method CHECK (code_challenge_method = 'S256'),
  CONSTRAINT chk_times  CHECK (expires_at > issued_at),
  CONSTRAINT chk_used   CHECK ((used = FALSE AND used_at IS NULL) OR (used = TRUE AND used_at IS NOT NULL))
);

-- ===== Indexes (avoid NOW() in predicates; avoid IF NOT EXISTS if your server chokes on it) =====

-- Authorization code: lookups by used/expires/code
DROP INDEX IF EXISTS oidcserver.idx_authcode_used_expires_code;
CREATE INDEX idx_authcode_used_expires_code
  ON oidcserver.authorization_code (used, expires_at, code);

-- Downstream login transaction: by status/expires
DROP INDEX IF EXISTS oidcserver.idx_login_tx_status_expires;
CREATE INDEX idx_login_tx_status_expires
  ON oidcserver.login_transaction (status, expires_at);

-- Upstream login transaction: by status/expires
DROP INDEX IF EXISTS oidcserver.idx_login_tx_up_status_expires;
CREATE INDEX idx_login_tx_up_status_expires
  ON oidcserver.login_transaction_upstream (status, expires_at);

-- Individual refresh token instances (rotated on every use)
CREATE TABLE IF NOT EXISTS oidcserver.refresh_token (
  token_id           UUID PRIMARY KEY,                -- server-generated
  family_id          UUID NOT NULL,                   -- FK → refresh_token_family
  status             TEXT NOT NULL,                   -- 'active'|'used'|'rotated'|'revoked'
  -- Fast lookup key (HMAC of token with server 'pepper'); safe to index
  lookup_key         BYTEA NOT NULL,                  -- 32 bytes (HMAC-SHA256)
  -- Verification material (slow hash)
  hash               BYTEA NOT NULL,                  -- PBKDF2-SHA256 output
  salt               BYTEA NOT NULL,                  -- 16–32 bytes
  iterations         INTEGER NOT NULL,                -- calibrated
  -- Binding / context
  client_id          TEXT NOT NULL,
  subject_id         TEXT NOT NULL,
  external_id        TEXT NULL,
  subject_party_uuid UUID,
  subject_party_id   INTEGER,
  subject_user_id    INTEGER,
  subject_user_name    TEXT,
  op_sid             TEXT NOT NULL,
  scopes             TEXT[] NOT NULL,
  acr                TEXT,
  amr                TEXT[],
  provider_claims      JSONB NULL,
  auth_time          TIMESTAMPTZ,
  -- Lifetimes
  created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at         TIMESTAMPTZ NOT NULL,            -- sliding window expiry
  absolute_expires_at TIMESTAMPTZ NOT NULL,           -- hard cap
  -- Rotation/revocation
  rotated_to_token_id UUID,
  revoked_at         TIMESTAMPTZ,
  revoked_reason     TEXT,
  -- Diagnostics
  user_agent_hash    TEXT,
  ip_hash            TEXT,

  CONSTRAINT fk_token_family
    FOREIGN KEY (family_id) REFERENCES oidcserver.refresh_token_family (family_id) ON DELETE CASCADE,
  CONSTRAINT fk_token_session
    FOREIGN KEY (op_sid) REFERENCES oidcserver.oidc_session (sid) ON DELETE CASCADE,
  CONSTRAINT chk_status
    CHECK (status IN ('active','used','rotated','revoked')),
  CONSTRAINT chk_times
    CHECK (absolute_expires_at >= expires_at AND expires_at > created_at)
);

-- Indexes
CREATE UNIQUE INDEX IF NOT EXISTS uq_refresh_lookup_key ON oidcserver.refresh_token (lookup_key);
CREATE INDEX IF NOT EXISTS idx_refresh_family_status ON oidcserver.refresh_token (family_id, status);
CREATE INDEX IF NOT EXISTS idx_refresh_op_sid ON oidcserver.refresh_token (op_sid);
CREATE INDEX IF NOT EXISTS idx_refresh_client_subject ON oidcserver.refresh_token (client_id, subject_id);
-- Family groups a rotating set of refresh tokens for one OP session + client
CREATE TABLE IF NOT EXISTS oidcserver.refresh_token_family (
  family_id      UUID PRIMARY KEY,                -- server-generated
  client_id      TEXT NOT NULL,
  subject_id     TEXT NOT NULL,
  op_sid         TEXT NOT NULL,                   -- FK → oidcserver.oidc_session.sid
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  revoked_at     TIMESTAMPTZ,
  revoked_reason TEXT,

  CONSTRAINT chk_revoked_consistency
    CHECK (
      (revoked_at IS NULL  AND revoked_reason IS NULL) OR
      (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)
    ),

  CONSTRAINT fk_family_session
    FOREIGN KEY (op_sid) REFERENCES oidcserver.oidc_session (sid) ON DELETE CASCADE
);

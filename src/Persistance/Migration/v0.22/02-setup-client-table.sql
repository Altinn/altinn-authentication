CREATE TABLE IF NOT EXISTS oidcserver.client (
  client_id                   TEXT PRIMARY KEY,
  client_name                 TEXT NOT NULL,
  client_type                 TEXT NOT NULL,
  token_endpoint_auth_method  TEXT NOT NULL,
  redirect_uris               TEXT[] NOT NULL,
  allowed_scopes              TEXT[] NOT NULL,
  client_secret_hash          TEXT,
  client_secret_expires_at    TIMESTAMPTZ,
  secret_rotation_at          TIMESTAMPTZ,
  jwks_uri                    TEXT,
  jwks                        JSONB,
  created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at                  TIMESTAMPTZ,
  CONSTRAINT chk_client_type
    CHECK (client_type IN ('Confidential','Public','Mtls','PrivateKeyJwtOnly')),
  CONSTRAINT chk_token_endpoint_auth_method
    CHECK (token_endpoint_auth_method IN ('ClientSecretBasic','ClientSecretPost','PrivateKeyJwt','None')),
  CONSTRAINT chk_redirect_uris_nonempty
    CHECK (redirect_uris IS NOT NULL AND array_length(redirect_uris, 1) >= 1),
  CONSTRAINT chk_allowed_scopes_nonempty
    CHECK (allowed_scopes IS NOT NULL AND array_length(allowed_scopes, 1) >= 1)
) 

TABLESPACE pg_default;
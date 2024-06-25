ALTER TABLE altinn_authentication_integration.system_user_integration
ADD COLUMN IF NOT EXISTS party_org_no varchar(255) NOT NULL DEFAULT 'new_column';


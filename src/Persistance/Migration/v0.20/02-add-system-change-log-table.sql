
CREATE TYPE business_application.systemchange_type AS ENUM(
  'create',
  'update',
  'rightsupdate',
  'accesspackageupdate',
  'delete',
  'unknown'
);

-- Table: business_application.system_change_log

-- DROP TABLE IF EXISTS business_application.system_change_log;

CREATE TABLE IF NOT EXISTS business_application.system_change_log
(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    system_internal_id UUID NOT NULL,
    changedby_orgnumber VARCHAR(255),
    change_type business_application.systemchange_type NOT NULL,
    changed_data JSONB NOT NULL,
    client_id TEXT,
    created timestamp with time zone NOT NULL,
    CONSTRAINT fk_system_internal_id
        FOREIGN KEY(system_internal_id)
        REFERENCES business_application.system_register(system_internal_id)
);

CREATE INDEX IF NOT EXISTS idx_system_change_log_system_internal_id
    ON business_application.system_change_log(system_internal_id);
-- Table: business_application.system_register_resource

-- DROP TABLE IF EXISTS business_application.system_register_resource;

CREATE TABLE IF NOT EXISTS business_application.system_register_resource
(
    system_internal_id uuid PRIMARY KEY NOT NULL REFERENCES business_application.system_register (system_internal_id) ON UPDATE CASCADE ON DELETE CASCADE,
    resource_registry_id text NOT NULL
)
TABLESPACE pg_default;
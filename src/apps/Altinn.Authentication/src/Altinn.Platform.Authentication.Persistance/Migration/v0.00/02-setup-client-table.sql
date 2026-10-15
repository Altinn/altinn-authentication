-- Table: business_application.maskinporten_client

-- DROP TABLE IF EXISTS business_application.maskinporten_client;

CREATE TABLE IF NOT EXISTS business_application.maskinporten_client
(
    client_id uuid PRIMARY KEY,    
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_deleted boolean DEFAULT false
)

TABLESPACE pg_default;
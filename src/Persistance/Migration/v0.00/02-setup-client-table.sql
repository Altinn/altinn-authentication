-- Table: business_application.maskinporten_client

-- DROP TABLE IF EXISTS business_application.maskinporten_client;

CREATE TABLE IF NOT EXISTS business_application.maskinporten_client
(
    client_id uuid NOT NULL,    
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_deleted boolean DEFAULT false
    CONSTRAINT maskinporten_client_pkey PRIMARY KEY (client_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS business_application.maskinporten_client
    OWNER to auth_authentication_admin;

GRANT ALL ON TABLE business_application.maskinporten_client TO auth_authentication;

GRANT ALL ON TABLE business_application.maskinporten_client TO auth_authentication_admin;
